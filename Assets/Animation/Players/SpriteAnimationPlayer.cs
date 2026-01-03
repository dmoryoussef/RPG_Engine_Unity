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
        private bool _suppressStepThisFrame;

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

        public void SetMirror(bool mirrorX, bool mirrorY)
        {
            if (!spriteRenderer) return;
            spriteRenderer.flipX = mirrorX;
            spriteRenderer.flipY = mirrorY;
        }

        // -------------------------
        // Playback
        // -------------------------

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

            // Ensure the first pose actually renders at least once before stepping.
            _suppressStepThisFrame = true;
        }

        public void Play(AnimationClipDef clipDef)
        {
            if (clipDef == null) return;
            Play(clipDef, playInReverse: false, loop: clipDef.loop, forceRestart: clipDef.restartOnEnter);
        }

        public void Play(AnimationClipDef clipDef, bool playInReverse, bool loop, bool forceRestart)
        {
            if (clipDef == null)
                return;

            var frames = clipDef.GetResolvedFrames();
            float fps = clipDef.GetResolvedFps();

            if (frames == null || frames.Length == 0 || fps <= 0f)
                return;

            int id = clipDef.GetStableId();

            var clip = new ClipData(
                id: id,
                frames: frames,
                fps: fps,
                loop: loop,
                restartOnEnter: forceRestart ? true : clipDef.restartOnEnter,
                playInReverse: playInReverse
            );

            Play(in clip);
        }

        public void Stop(bool clearSprite = false)
        {
            _hasClip = false;
            _frameIndex = 0;
            _frameTimer = 0f;
            _suppressStepThisFrame = false;

            if (clearSprite && spriteRenderer)
                spriteRenderer.sprite = null;
        }

        /// <summary>
        /// Sets the renderer sprite to the "start pose" of a clip without starting playback.
        /// Forward => first non-null frame, Reverse => last non-null frame.
        /// </summary>
        public bool TrySetPose(AnimationClipDef clipDef, bool playInReverse)
        {
            if (!spriteRenderer)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            if (!spriteRenderer || clipDef == null)
                return false;

            var frames = clipDef.GetResolvedFrames();
            if (frames == null || frames.Length == 0)
                return false;

            if (!playInReverse)
            {
                for (int i = 0; i < frames.Length; i++)
                {
                    if (frames[i] != null)
                    {
                        spriteRenderer.sprite = frames[i];
                        return true;
                    }
                }
            }
            else
            {
                for (int i = frames.Length - 1; i >= 0; i--)
                {
                    if (frames[i] != null)
                    {
                        spriteRenderer.sprite = frames[i];
                        return true;
                    }
                }
            }

            return false;
        }

        private void Update()
        {
            if (!IsPlaying) return;

            if (_suppressStepThisFrame)
            {
                _suppressStepThisFrame = false;
                return;
            }

            _frameTimer += Time.deltaTime;
            float frameDuration = 1f / _current.fps;

            while (_frameTimer >= frameDuration)
            {
                _frameTimer -= frameDuration;
                Step();

                // If Step stopped playback (non-loop terminal), stop processing this frame.
                if (!_hasClip)
                    break;
            }
        }

        private void Step()
        {
            if (_current.frames == null || _current.frames.Length == 0)
                return;

            int lastIndex = _current.frames.Length - 1;

            if (!_playInReverse)
            {
                // Forward
                int next = _frameIndex + 1;

                if (next > lastIndex)
                {
                    if (_current.loop)
                    {
                        _frameIndex = 0;
                        ApplyFrame();
                    }
                    else
                    {
                        _frameIndex = lastIndex;
                        ApplyFrame();
                        _hasClip = false; // STOP on terminal frame
                    }
                    return;
                }

                _frameIndex = next;
                ApplyFrame();
                return;
            }

            // Reverse
            {
                int next = _frameIndex - 1;

                if (next < 0)
                {
                    if (_current.loop)
                    {
                        _frameIndex = lastIndex;
                        ApplyFrame();
                    }
                    else
                    {
                        _frameIndex = 0;
                        ApplyFrame();
                        _hasClip = false; // STOP on terminal frame
                    }
                    return;
                }

                _frameIndex = next;
                ApplyFrame();
            }
        }

        private void ApplyFrame()
        {
            if (!spriteRenderer || _current.frames == null || _current.frames.Length == 0)
                return;

            int count = _current.frames.Length;

            // LOOPING: wrapping scan is fine.
            if (_current.loop)
            {
                for (int i = 0; i < count; i++)
                {
                    int idx = _playInReverse
                        ? ((_frameIndex - i) % count + count) % count
                        : (_frameIndex + i) % count;

                    var s = _current.frames[idx];
                    if (s != null)
                    {
                        spriteRenderer.sprite = s;
                        _frameIndex = idx;
                        return;
                    }
                }

                Debug.LogWarning($"[{name}] Clip '{_current.id}' has {count} frames but all are NULL sprites.");
                return;
            }

            // NON-LOOPING: never wrap. This prevents “reverse looks like it loops”.
            if (!_playInReverse)
            {
                // Forward: try current index, then forward to the end.
                for (int idx = _frameIndex; idx < count; idx++)
                {
                    var s = _current.frames[idx];
                    if (s != null)
                    {
                        spriteRenderer.sprite = s;
                        _frameIndex = idx;
                        return;
                    }
                }
            }
            else
            {
                // Reverse: try current index, then backward to 0.
                for (int idx = _frameIndex; idx >= 0; idx--)
                {
                    var s = _current.frames[idx];
                    if (s != null)
                    {
                        spriteRenderer.sprite = s;
                        _frameIndex = idx;
                        return;
                    }
                }
            }

            Debug.LogWarning($"[{name}] Non-loop clip '{_current.id}' has NULL sprites near terminal pose (frame {_frameIndex}).");
        }

        // -------------------------
        // Data
        // -------------------------

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
