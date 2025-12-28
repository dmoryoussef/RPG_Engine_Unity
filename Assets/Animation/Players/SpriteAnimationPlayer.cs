using UnityEngine;

namespace Animation
{
    [DisallowMultipleComponent]
    public sealed class SpriteAnimationPlayer : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;

        private ClipData _current;
        private bool _hasClip;

        private int _frameIndex;
        private float _frameTimer;

        private bool _playInReverse;

        public ClipData Current => _current;
        public bool IsPlaying => _hasClip && _current.frames != null && _current.frames.Length > 0;

        private void Reset()
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        private void Awake()
        {
            if (!spriteRenderer)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        public void SetMirror(MirrorInstruction mirror)
        {
            if (!spriteRenderer) return;

            // MVP: map mirror intent onto SpriteRenderer flips.
            switch (mirror)
            {
                case MirrorInstruction.None:
                    spriteRenderer.flipX = false;
                    spriteRenderer.flipY = false;
                    break;
                case MirrorInstruction.Horizontal:
                    spriteRenderer.flipX = true;
                    spriteRenderer.flipY = false;
                    break;
                case MirrorInstruction.Vertical:
                    spriteRenderer.flipX = false;
                    spriteRenderer.flipY = true;
                    break;
                case MirrorInstruction.Both:
                    spriteRenderer.flipX = true;
                    spriteRenderer.flipY = true;
                    break;
            }
        }

        /// <summary>
        /// Lowest-level playback entry. All systems can feed the player via ClipData.
        /// </summary>
        public void Play(in ClipData clip)
        {
            if (clip.frames == null || clip.frames.Length == 0 || clip.fps <= 0f)
                return;

            bool sameIdentity = _hasClip && _current.id == clip.id && _playInReverse == clip.playInReverse;

            // If same identity and restart not requested, don't restart.
            if (sameIdentity && !clip.restartOnEnter)
                return;

            _current = clip;
            _hasClip = true;

            _playInReverse = clip.playInReverse;

            // Start frame depends on direction
            _frameIndex = _playInReverse ? (_current.frames.Length - 1) : 0;
            _frameTimer = 0f;

            ApplyFrame();
        }

        /// <summary>
        /// Convenience: play an AnimationClipDef with optional reverse playback and optional forced restart.
        /// </summary>
        public void Play(AnimationClipDef clipDef, bool playInReverse, bool forceRestart)
        {
            if (clipDef == null)
                return;

            var frames = clipDef.GetResolvedFrames();

            if (frames == null || frames.Length == 0)
            {
                Debug.LogWarning(
                    $"[{name}] No resolved frames for clip '{clipDef.name}'. " +
                    $"Source={clipDef.source}, UnityClip={(clipDef.unityClip ? clipDef.unityClip.name : "null")}"
                );
                return;
            }

            float fps = clipDef.GetResolvedFps();
            if (fps <= 0f)
                return;

            int id = clipDef.GetStableId();

            var clip = new ClipData(
                id: id,
                frames: frames,
                fps: fps,
                loop: clipDef.loop,
                restartOnEnter: forceRestart ? true : clipDef.restartOnEnter,
                playInReverse: playInReverse
            );

            Play(in clip);
        }

        /// <summary>
        /// Backwards-compatible overload: same behavior as before (forward playback, respects clipDef.restartOnEnter).
        /// </summary>
        public void Play(AnimationClipDef clipDef)
        {
            Play(clipDef, playInReverse: false, forceRestart: false);
        }

        public void Stop(bool clearSprite = false)
        {
            _hasClip = false;
            _frameIndex = 0;
            _frameTimer = 0f;

            if (clearSprite && spriteRenderer)
                spriteRenderer.sprite = null;
        }

        private void Update()
        {
            if (!IsPlaying) return;

            _frameTimer += Time.deltaTime;
            float frameDuration = 1f / _current.fps;

            while (_frameTimer >= frameDuration)
            {
                _frameTimer -= frameDuration;
                Step();
            }
        }

        private void Step()
        {
            int next = _frameIndex + (_playInReverse ? -1 : 1);

            if (!_playInReverse)
            {
                // Forward
                if (next >= _current.frames.Length)
                    next = _current.loop ? 0 : _current.frames.Length - 1;
            }
            else
            {
                // Reverse
                if (next < 0)
                    next = _current.loop ? (_current.frames.Length - 1) : 0;
            }

            _frameIndex = next;
            ApplyFrame();
        }

        private void ApplyFrame()
        {
            if (!spriteRenderer || _current.frames == null || _current.frames.Length == 0)
                return;

            int count = _current.frames.Length;

            // Try the current frame first, then scan in playback direction to find the next non-null sprite.
            for (int i = 0; i < count; i++)
            {
                int idx = _playInReverse
                    ? ((_frameIndex - i) % count + count) % count
                    : (_frameIndex + i) % count;

                var s = _current.frames[idx];
                if (s != null)
                {
                    spriteRenderer.sprite = s;

                    // Snap to the sprite we actually displayed.
                    _frameIndex = idx;
                    return;
                }
            }

            Debug.LogWarning($"[{name}] Clip '{_current.id}' has {count} frames but all are NULL sprites.");
        }

        public bool TrySetPose(AnimationClipDef clipDef, bool playInReverse)
        {
            if (!spriteRenderer || clipDef == null)
                return false;

            var frames = clipDef.GetResolvedFrames();
            if (frames == null || frames.Length == 0)
                return false;

            int count = frames.Length;

            if (!playInReverse)
            {
                for (int i = 0; i < count; i++)
                {
                    var s = frames[i];
                    if (s != null)
                    {
                        spriteRenderer.sprite = s;
#if UNITY_EDITOR
                        UnityEditor.EditorUtility.SetDirty(spriteRenderer);
#endif
                        return true;
                    }
                }
            }
            else
            {
                for (int i = count - 1; i >= 0; i--)
                {
                    var s = frames[i];
                    if (s != null)
                    {
                        spriteRenderer.sprite = s;
#if UNITY_EDITOR
                        UnityEditor.EditorUtility.SetDirty(spriteRenderer);
#endif
                        return true;
                    }
                }
            }

            return false;
        }


        // Shared payload type: this is how ALL systems feed the player.
        public readonly struct ClipData
        {
            public readonly int id;              // stable identity for "same clip" checks
            public readonly Sprite[] frames;
            public readonly float fps;
            public readonly bool loop;
            public readonly bool restartOnEnter;
            public readonly bool playInReverse;

            public ClipData(int id, Sprite[] frames, float fps, bool loop, bool restartOnEnter, bool playInReverse)
            {
                this.id = id;
                this.frames = frames;
                this.fps = fps;
                this.loop = loop;
                this.restartOnEnter = restartOnEnter;
                this.playInReverse = playInReverse;
            }
        }
    }
}
