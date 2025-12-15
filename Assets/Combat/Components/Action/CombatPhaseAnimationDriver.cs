using UnityEngine;

namespace Combat
{
    [ExecuteAlways]
    [AddComponentMenu("Combat/Animation/Combat Phase Sprite Animation Driver")]
    public sealed class CombatPhaseSpriteAnimationDriver : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private ActionTimelineController controller;
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Animation Data")]
        [SerializeField] private ActionSpriteAnimationDef animationDef;

        [Header("Debug")]
        [SerializeField] private bool log = false;

        // Runtime playback state
        ActionSpriteAnimationDef.PhaseClip _clip;
        int _frameIndex;
        float _frameTimer;

        // Runtime restore (null-check approach)
        Sprite _cachedBaseSprite;

        // Editor preview state
        [Header("Editor Preview")]
        [SerializeField] private bool previewMode = false;
        [SerializeField] private ActionPhase previewPhase = ActionPhase.Active;
        [SerializeField] private int previewFrame = 0;

        // We cache what was displayed *before* preview so toggling off restores it.
        Sprite _cachedPreviewSprite;
        bool _wasPreviewMode;

        private void Reset()
        {
            if (!controller) controller = GetComponent<ActionTimelineController>();
            if (!spriteRenderer) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        private void Awake()
        {
            if (!controller) controller = GetComponent<ActionTimelineController>();
            if (!spriteRenderer) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        private void OnEnable()
        {
            // In edit mode we don't want to subscribe to runtime events.
            if (!Application.isPlaying)
                return;

            if (!controller)
            {
                Debug.LogWarning("[CombatPhaseSpriteAnimationDriver] No ActionTimelineController assigned/found.", this);
                return;
            }

            controller.OnPhaseEnter += OnPhaseEnter;
            controller.OnPhaseExit += OnPhaseExit;
            controller.OnFinished += OnFinished;
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
                return;

            if (!controller) return;

            controller.OnPhaseEnter -= OnPhaseEnter;
            controller.OnPhaseExit -= OnPhaseExit;
            controller.OnFinished -= OnFinished;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!spriteRenderer) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (!controller) controller = GetComponent<ActionTimelineController>();

            // Detect preview toggle change and restore properly when turning OFF.
            if (_wasPreviewMode && !previewMode)
            {
                RestoreAfterPreview();
            }

            _wasPreviewMode = previewMode;

            // Apply preview immediately when editing values in inspector.
            if (!Application.isPlaying)
            {
                if (previewMode)
                    ApplyPreviewSprite();
            }
        }
#endif

        private void Update()
        {
            // Editor-only preview driver
            if (!Application.isPlaying)
            {
                if (previewMode)
                    ApplyPreviewSprite();

                return;
            }

            // Runtime animation playback
            if (_clip == null || spriteRenderer == null) return;

            var frames = _clip.frames;
            if (frames == null || frames.Length == 0) return;

            float fps = Mathf.Max(1f, _clip.fps);
            float frameDuration = 1f / fps;

            _frameTimer += Time.deltaTime;
            while (_frameTimer >= frameDuration)
            {
                _frameTimer -= frameDuration;
                _frameIndex++;

                if (_frameIndex >= frames.Length)
                {
                    if (_clip.loop) _frameIndex = 0;
                    else { _frameIndex = frames.Length - 1; break; }
                }
            }

            spriteRenderer.sprite = frames[_frameIndex];
        }

        private void ApplyPreviewSprite()
        {
            if (spriteRenderer == null || animationDef == null) return;

            // Cache the sprite that was showing BEFORE preview, once.
            if (_cachedPreviewSprite == null)
                _cachedPreviewSprite = spriteRenderer.sprite;

            if (!animationDef.TryGet(previewPhase, out var clip) || clip == null) return;
            if (clip.frames == null || clip.frames.Length == 0) return;

            int idx = Mathf.Clamp(previewFrame, 0, clip.frames.Length - 1);
            spriteRenderer.sprite = clip.frames[idx];
        }

        private void RestoreAfterPreview()
        {
            if (spriteRenderer && _cachedPreviewSprite)
                spriteRenderer.sprite = _cachedPreviewSprite;

            _cachedPreviewSprite = null;
        }

        private void OnPhaseEnter(ActionPhase phase)
        {
            if (!_cachedBaseSprite && spriteRenderer)
                _cachedBaseSprite = spriteRenderer.sprite;

            if (animationDef == null) return;

            if (!animationDef.TryGet(phase, out _clip) || _clip == null)
            {
                _clip = null;
                return;
            }

            if (log) Debug.Log($"[SpriteAnim] Enter {phase}", this);

            if (_clip.restartOnEnter)
            {
                _frameIndex = 0;
                _frameTimer = 0f;
            }

            if (spriteRenderer && _clip.frames != null && _clip.frames.Length > 0)
                spriteRenderer.sprite = _clip.frames[0];
        }

        private void OnPhaseExit(ActionPhase phase)
        {
            if (log) Debug.Log($"[SpriteAnim] Exit {phase}", this);
        }

        private void OnFinished()
        {
            if (log) Debug.Log("[SpriteAnim] Finished", this);

            if (spriteRenderer && _cachedBaseSprite)
                spriteRenderer.sprite = _cachedBaseSprite;

            _cachedBaseSprite = null;

            _clip = null;
            _frameIndex = 0;
            _frameTimer = 0f;
        }
    }
}
