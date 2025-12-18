using UnityEngine;
using Combat;

namespace Animation
{
    [DisallowMultipleComponent]
    public sealed class ActorVisualController : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private ActionTimelineController actionTimeline;
        [SerializeField] private SpriteAnimationPlayer player;

        [Header("Facing (MVP)")]
        [SerializeField] private FacingProvider facingProvider;
        [SerializeField] private bool defaultFacingRight = true;

        [Header("Locomotion Visuals (MVP)")]
        [SerializeField] private DirectionalClipMap idleVisual;
        [SerializeField] private DirectionalClipMap walkVisual;
        [SerializeField] private DirectionalClipMap runVisual;

        [SerializeField] private float walkSpeedThreshold = 0.1f;
        [SerializeField] private float runSpeedThreshold = 3.0f;

        // -------------------------
        // Debug (Read Only)
        // -------------------------
        [Header("Debug (Read Only)")]
        [SerializeField] private bool debugActionActive;
        [SerializeField] private string debugLayer;               // "Action" or "Locomotion"
        [SerializeField] private string debugResolvedClipName;
        [SerializeField] private MirrorInstruction debugResolvedMirror;
        [SerializeField] private string debugLocomotionTier;      // "Idle/Walk/Run"
        [SerializeField] private float debugSpeed;

        // Combat state
        private ActionPhase _currentPhase;
        private bool _actionActive;

        // Locomotion sampling
        private Vector3 _lastPosition;
        private bool _lastFacingRight;

        // Playback caching (prevents constant restarts)
        private AnimationClipDef _lastClip;
        private MirrorInstruction _lastMirror;

        private void Reset()
        {
            actionTimeline = GetComponent<ActionTimelineController>();
            player = GetComponentInChildren<SpriteAnimationPlayer>();
            facingProvider = GetComponent<FacingProvider>();
        }

        private void Awake()
        {
            if (!actionTimeline) actionTimeline = GetComponent<ActionTimelineController>();
            if (!player) player = GetComponentInChildren<SpriteAnimationPlayer>();
            if (!facingProvider) facingProvider = GetComponent<FacingProvider>();

            _lastPosition = transform.position;
            _lastFacingRight = facingProvider ? facingProvider.FacingRight : defaultFacingRight;

            _lastClip = null;
            _lastMirror = MirrorInstruction.None;

            // Init debug
            debugLayer = "";
            debugResolvedClipName = "";
            debugResolvedMirror = MirrorInstruction.None;
            debugLocomotionTier = "";
            debugSpeed = 0f;
        }

        private void OnEnable()
        {
            if (!actionTimeline) return;

            actionTimeline.OnPhaseEnter += HandlePhaseEnter;
            actionTimeline.OnFinished += HandleFinished;
        }

        private void OnDisable()
        {
            if (!actionTimeline) return;

            actionTimeline.OnPhaseEnter -= HandlePhaseEnter;
            actionTimeline.OnFinished -= HandleFinished;
        }

        private void Update()
        {
            debugActionActive = _actionActive;

            if (_actionActive)
            {
                // Keep locomotion sampling stable even during actions
                _lastPosition = transform.position;
                return;
            }

            TryPlayLocomotionVisual();
        }

        private void HandlePhaseEnter(ActionPhase phase)
        {
            _currentPhase = phase;
            _actionActive = true;

            debugLayer = "Action";
            debugLocomotionTier = "";
            debugSpeed = 0f;

            TryPlayCurrentActionVisual();
        }

        private void HandleFinished()
        {
            _actionActive = false;

            // Locomotion will take over next Update.
            _lastClip = null;
            _lastMirror = MirrorInstruction.None;

            debugLayer = "Locomotion";
        }

        private void TryPlayCurrentActionVisual()
        {
            if (!player || !actionTimeline)
                return;

            MoveDef move = actionTimeline.CurrentActionDef;
            if (!move || move.phases == null || move.phases.Count == 0)
                return;

            MoveDef.Phase phaseEntry = null;
            for (int i = 0; i < move.phases.Count; i++)
            {
                if (move.phases[i].phaseId == _currentPhase)
                {
                    phaseEntry = move.phases[i];
                    break;
                }
            }
            if (phaseEntry == null || phaseEntry.visuals == null)
                return;

            bool facingRight = facingProvider ? facingProvider.FacingRight : defaultFacingRight;

            if (phaseEntry.visuals.TryResolve(facingRight, out var clip, out var mirror) && clip != null)
            {
                debugResolvedClipName = clip.name;
                debugResolvedMirror = mirror;

                ApplyIfChanged(clip, mirror);
            }
        }

        private void TryPlayLocomotionVisual()
        {
            if (!player)
                return;

            debugLayer = "Locomotion";

            Vector3 currentPos = transform.position;
            Vector3 delta = currentPos - _lastPosition;

            float speed = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            debugSpeed = speed;

            if (Mathf.Abs(delta.x) > 0.001f)
                _lastFacingRight = delta.x > 0f;

            bool facingRight = _lastFacingRight;

            DirectionalClipMap map;
            if (speed < walkSpeedThreshold)
            {
                map = idleVisual;
                debugLocomotionTier = "Idle";
            }
            else if (speed < runSpeedThreshold)
            {
                map = walkVisual;
                debugLocomotionTier = "Walk";
            }
            else
            {
                map = runVisual;
                debugLocomotionTier = "Run";
            }

            if (map != null && map.TryResolve(facingRight, out var clip, out var mirror) && clip != null)
            {
                debugResolvedClipName = clip.name;
                debugResolvedMirror = mirror;

                ApplyIfChanged(clip, mirror);
            }

            _lastPosition = currentPos;
        }

        private void ApplyIfChanged(AnimationClipDef clip, MirrorInstruction mirror)
        {
            if (_lastClip == clip && _lastMirror == mirror)
                return;

            _lastClip = clip;
            _lastMirror = mirror;

            player.SetMirror(mirror);
            player.Play(clip);
        }
    }
}
