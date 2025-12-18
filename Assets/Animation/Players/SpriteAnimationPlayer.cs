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
            // (Z flip isn’t meaningful for SpriteRenderer; future backends can reinterpret this.)
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

        public void Play(in ClipData clip)
        {
            if (clip.frames == null || clip.frames.Length == 0 || clip.fps <= 0f)
                return;

            // If same "identity" and restart not requested, don't restart.
            if (_hasClip && _current.id == clip.id && !clip.restartOnEnter)
                return;

            _current = clip;
            _hasClip = true;

            _frameIndex = 0;
            _frameTimer = 0f;

            ApplyFrame();
        }

        public void Play(AnimationClipDef clipDef)
        {
            if (clipDef == null)
                return;

            // MVP: manual frames only. If frames missing, do nothing (hold-last behavior happens naturally).
            if (clipDef.frames == null || clipDef.frames.Length == 0 || clipDef.fps <= 0f)
                return;

            // Stable identity: instance id is enough for MVP (unique per asset instance).
            int id = clipDef.GetInstanceID();

            var clip = new ClipData(
                id: id,
                frames: clipDef.frames,
                fps: clipDef.fps,
                loop: clipDef.loop,
                restartOnEnter: clipDef.restartOnEnter
            );

            Play(in clip);
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
            int next = _frameIndex + 1;

            if (next >= _current.frames.Length)
                next = _current.loop ? 0 : _current.frames.Length - 1;

            _frameIndex = next;
            ApplyFrame();
        }

        private void ApplyFrame()
        {
            if (!spriteRenderer || _current.frames == null || _current.frames.Length == 0)
                return;

            var s = _current.frames[Mathf.Clamp(_frameIndex, 0, _current.frames.Length - 1)];
            if (s) spriteRenderer.sprite = s;
        }

        // Shared payload type: this is how ALL systems feed the player.
        public readonly struct ClipData
        {
            public readonly int id;              // stable identity for "same clip" checks
            public readonly Sprite[] frames;
            public readonly float fps;
            public readonly bool loop;
            public readonly bool restartOnEnter;

            public ClipData(int id, Sprite[] frames, float fps, bool loop, bool restartOnEnter)
            {
                this.id = id;
                this.frames = frames;
                this.fps = fps;
                this.loop = loop;
                this.restartOnEnter = restartOnEnter;
            }
        }
    }
}
