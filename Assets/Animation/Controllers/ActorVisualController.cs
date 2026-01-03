using UnityEngine;
using Combat;
using Player;

namespace Animation
{
    [DisallowMultipleComponent]
    public sealed class ActorVisualController : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private ActionTimelineController actionTimeline;
        [SerializeField] private SpriteAnimationPlayer player;

        [Header("Mover2D (Visual Driver)")]
        [SerializeField] private PlayerMover2D mover2D;

        [Tooltip("Fallback facing when mover has no facing yet (e.g. idle at spawn).")]
        [SerializeField] private bool defaultFacingRight = true;

        [Header("Locomotion Visuals (MVP)")]
        [SerializeField] private DirectionalVisualSet idleVisual;
        [SerializeField] private DirectionalVisualSet walkVisual;
        [SerializeField] private DirectionalVisualSet runVisual;

        [Header("Locomotion Thresholds")]
        [SerializeField] private float walkSpeedThreshold = 0.1f;
        [SerializeField] private float runSpeedThreshold = 3.0f;

        [Header("Debug (Read Only)")]
        [SerializeField] private bool debugActionActive;
        [SerializeField] private string debugLayer;
        [SerializeField] private string debugResolvedClipName;
        [SerializeField] private bool debugMirrorX;
        [SerializeField] private bool debugMirrorY;

        private ActionPhase _currentPhase;
        private bool _actionActive;

        // Cache to avoid restart spam
        private int _lastClipId;
        private bool _lastMirrorX;
        private bool _lastMirrorY;
        private bool _lastReverse;

        private void Reset()
        {
            actionTimeline = GetComponent<ActionTimelineController>();
            player = GetComponentInChildren<SpriteAnimationPlayer>();
            mover2D = GetComponent<PlayerMover2D>();
        }

        private void Awake()
        {
            if (!actionTimeline) actionTimeline = GetComponent<ActionTimelineController>();
            if (!player) player = GetComponentInChildren<SpriteAnimationPlayer>();
            if (!mover2D) mover2D = GetComponent<PlayerMover2D>();

            _lastClipId = 0;
            _lastMirrorX = false;
            _lastMirrorY = false;
            _lastReverse = false;

            debugLayer = "";
            debugResolvedClipName = "";
            debugMirrorX = false;
            debugMirrorY = false;
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
                return;

            TryPlayLocomotionVisual();
        }

        private void HandlePhaseEnter(ActionPhase phase)
        {
            _currentPhase = phase;
            _actionActive = true;
            debugLayer = "Action";

            TryPlayCurrentActionVisual();
        }

        private void HandleFinished()
        {
            _actionActive = false;

            // Reset cache so locomotion can apply immediately
            _lastClipId = 0;
            _lastMirrorX = false;
            _lastMirrorY = false;
            _lastReverse = false;

            debugLayer = "Locomotion";
        }

        private Vector2 ResolveFacing()
        {
            Vector2 f = mover2D ? mover2D.Facing : Vector2.zero;

            if (f.sqrMagnitude < 0.0001f)
                return defaultFacingRight ? Vector2.right : Vector2.left;

            return f;
        }

        private void TryPlayCurrentActionVisual()
        {
            if (!player || !actionTimeline)
                return;

            MoveDef move = actionTimeline.CurrentActionDef;
            if (!move || move.phases == null || move.phases.Count == 0 || move.moveVisuals == null)
                return;

            // Find phase entry
            MoveDef.Phase phaseEntry = null;
            for (int i = 0; i < move.phases.Count; i++)
            {
                if (move.phases[i].phaseId == _currentPhase)
                {
                    phaseEntry = move.phases[i];
                    break;
                }
            }
            if (phaseEntry == null)
                return;

            Vector2 facing = ResolveFacing();

            if (!move.moveVisuals.TryGetVisualForDirection(facing, out var vis))
                return;

            if (vis.clip == null)
                return;

            debugResolvedClipName = vis.clip.name;
            debugMirrorX = vis.mirrorX;
            debugMirrorY = vis.mirrorY;

            player.SetMirror(vis.mirrorX, vis.mirrorY);

            // Action playback (phase slicing) logic would go here if you’re slicing.
            // For now, play the resolved clip normally (forward).
            player.Play(vis.clip);
        }

        private void TryPlayLocomotionVisual()
        {
            if (!player)
                return;

            debugLayer = "Locomotion";

            var locomotion = mover2D ? mover2D.CurrentLocomotion : PlayerMover2D.LocomotionState.Idle;

            DirectionalVisualSet set = locomotion switch
            {
                PlayerMover2D.LocomotionState.Idle => idleVisual,
                PlayerMover2D.LocomotionState.Walk => walkVisual,
                PlayerMover2D.LocomotionState.Run => runVisual,
                _ => idleVisual
            };

            if (set == null)
                return;

            Vector2 facing = ResolveFacing();

            if (!set.TryGetVisualForDirection(facing, out var vis) || vis.clip == null)
                return;

            debugResolvedClipName = vis.clip.name;
            debugMirrorX = vis.mirrorX;
            debugMirrorY = vis.mirrorY;

            // Build clip data for caching (forward, looping uses clipDef.loop)
            var frames = vis.clip.GetResolvedFrames();
            float fps = vis.clip.GetResolvedFps();
            if (frames == null || frames.Length == 0 || fps <= 0f)
                return;

            var clipData = new SpriteAnimationPlayer.ClipData(
                id: vis.clip.GetStableId(),
                frames: frames,
                fps: fps,
                loop: vis.clip.loop,
                restartOnEnter: vis.clip.restartOnEnter,
                playInReverse: false
            );

            ApplyIfChanged(in clipData, vis.mirrorX, vis.mirrorY, playInReverse: false);
        }

        private void ApplyIfChanged(in SpriteAnimationPlayer.ClipData clip, bool mirrorX, bool mirrorY, bool playInReverse)
        {
            if (_lastClipId == clip.id && _lastMirrorX == mirrorX && _lastMirrorY == mirrorY && _lastReverse == playInReverse)
                return;

            _lastClipId = clip.id;
            _lastMirrorX = mirrorX;
            _lastMirrorY = mirrorY;
            _lastReverse = playInReverse;

            player.SetMirror(mirrorX, mirrorY);
            player.Play(in clip);
        }
    }
}
