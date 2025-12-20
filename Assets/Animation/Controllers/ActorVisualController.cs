using UnityEngine;
using Combat;
using Player;

namespace Animation
{
    /// <summary>
    /// ActorVisualController
    ///
    /// Responsibility:
    /// - Decide WHICH visual clip should be playing (action vs locomotion).
    /// - Resolve directional clip + mirror instructions based on facing.
    /// - For combat actions: resolve ONE move-level visual (MoveDef.moveVisuals) and play only the phase slice.
    /// - Feed the resolved clip (or sliced sub-clip) into SpriteAnimationPlayer (which handles timing/frame stepping).
    ///
    /// Design choice:
    /// - This class does NOT compute movement deltas from transform.position.
    /// - It relies on PlayerMover2D as single source of truth for:
    ///     - facing direction (left/right)
    ///     - movement speed
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ActorVisualController : MonoBehaviour
    {
        // -------------------------
        // Wiring
        // -------------------------
        [Header("Wiring")]
        [SerializeField] private ActionTimelineController actionTimeline;
        [SerializeField] private SpriteAnimationPlayer player;

        [Header("Mover2D (Visual Driver)")]
        [Tooltip("Source-of-truth for facing + movement speed. Required for locomotion visuals.")]
        [SerializeField] private PlayerMover2D mover2D;

        [Tooltip("Fallback facing when mover has no horizontal facing yet (e.g. idle at spawn).")]
        [SerializeField] private bool defaultFacingRight = true;

        // -------------------------
        // Locomotion
        // -------------------------
        [Header("Locomotion Visuals (MVP)")]
        [SerializeField] private DirectionalVisualSet idleVisual;
        [SerializeField] private DirectionalVisualSet walkVisual;
        [SerializeField] private DirectionalVisualSet runVisual;

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

        [Header("Debug (Action Slice)")]
        [SerializeField] private int debugSliceStartFrame;
        [SerializeField] private int debugSliceEndFrame;
        [SerializeField] private float debugSliceFps;
        [SerializeField] private int debugResolvedFrameCount;

        // -------------------------
        // Internal state
        // -------------------------
        private ActionPhase _currentPhase;
        private bool _actionActive;

        private bool _lastFacingRight;

        // Playback caching (prevents constant restarts)
        private int _lastClipId;
        private MirrorInstruction _lastMirror;

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

            _lastFacingRight = ResolveFacingRightOrFallback();

            _lastClipId = 0;
            _lastMirror = MirrorInstruction.None;

            debugLayer = "";
            debugResolvedClipName = "";
            debugResolvedMirror = MirrorInstruction.None;
            debugLocomotionTier = "";
            debugSpeed = 0f;

            debugSliceStartFrame = 0;
            debugSliceEndFrame = 0;
            debugSliceFps = 0f;
            debugResolvedFrameCount = 0;
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

            _lastFacingRight = ResolveFacingRightOrFallback();

            if (_actionActive)
                return;

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

            // Reset cache so locomotion can immediately apply its current clip.
            _lastClipId = 0;
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

            // Find the phase entry
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

            // Resolve move-level visuals (single visual set for entire move)
            bool facingRight = _lastFacingRight;

            if (move.moveVisuals == null)
                return;

            if (!move.moveVisuals.TryResolve(facingRight, out var baseClipDef, out var mirror) || baseClipDef == null)
                return;

            // Build a sliced ClipData (subset of frames) for this phase
            if (!TryBuildSlicedClipData(
                    baseClipDef,
                    phaseEntry.visualSlice,
                    phaseEntry.durationMs,
                    out var clipData,
                    out string clipNameForDebug,
                    out int sliceStart,
                    out int sliceEnd,
                    out float fps,
                    out int resolvedFrameCount))
            {
                return;
            }

            debugResolvedClipName = clipNameForDebug;
            debugResolvedMirror = mirror;

            debugSliceStartFrame = sliceStart;
            debugSliceEndFrame = sliceEnd;
            debugSliceFps = fps;
            debugResolvedFrameCount = resolvedFrameCount;

            ApplyIfChanged(in clipData, mirror);
        }

        private void TryPlayLocomotionVisual()
        {
            if (!player)
                return;

            debugLayer = "Locomotion";

            var locomotion = mover2D ? mover2D.CurrentLocomotion : PlayerMover2D.LocomotionState.Idle;

            // fancy c# switch expression
            DirectionalVisualSet map = locomotion switch
            {
                PlayerMover2D.LocomotionState.Idle => idleVisual,
                PlayerMover2D.LocomotionState.Walk => walkVisual,
                PlayerMover2D.LocomotionState.Run => runVisual,
                _ => idleVisual
            };


            bool facingRight = _lastFacingRight;

            if (map != null && map.TryResolve(facingRight, out var clipDef, out var mirror) && clipDef != null)
            {
                // Locomotion plays full clip
                var frames = clipDef.GetResolvedFrames();
                float fps = clipDef.GetResolvedFps();
                if (frames == null || frames.Length == 0 || fps <= 0f)
                    return;

                int id = clipDef.GetStableId();

                var clip = new SpriteAnimationPlayer.ClipData(
                    id: id,
                    frames: frames,
                    fps: fps,
                    loop: clipDef.loop,
                    restartOnEnter: clipDef.restartOnEnter
                );

                debugResolvedClipName = clipDef.name;
                debugResolvedMirror = mirror;

                debugSliceStartFrame = 0;
                debugSliceEndFrame = frames.Length;
                debugSliceFps = fps;
                debugResolvedFrameCount = frames.Length;

                ApplyIfChanged(in clip, mirror);
            }
        }

        private bool ResolveFacingRightOrFallback()
        {
            bool facingRight = _lastFacingRight;

            if (mover2D)
            {
                Vector2 f = mover2D.Facing;

                if (Mathf.Abs(f.x) > 0.001f)
                    facingRight = f.x > 0f;
                else if (_lastClipId == 0)
                    facingRight = defaultFacingRight;
            }
            else if (_lastClipId == 0)
            {
                facingRight = defaultFacingRight;
            }

            return facingRight;
        }

        private void ApplyIfChanged(in SpriteAnimationPlayer.ClipData clip, MirrorInstruction mirror)
        {
            if (_lastClipId == clip.id && _lastMirror == mirror)
                return;

            _lastClipId = clip.id;
            _lastMirror = mirror;

            player.SetMirror(mirror);
            player.Play(in clip);
        }

        private static bool TryBuildSlicedClipData(
            AnimationClipDef baseClipDef,
            MoveDef.VisualSlice slice,
            int phaseDurationMs,
            out SpriteAnimationPlayer.ClipData outClip,
            out string outDebugName,
            out int outSliceStart,
            out int outSliceEnd,
            out float outFps,
            out int outResolvedFrameCount)
        {
            outClip = default;
            outDebugName = "";
            outSliceStart = 0;
            outSliceEnd = 0;
            outFps = 0f;
            outResolvedFrameCount = 0;

            if (!baseClipDef)
                return false;

            var frames = baseClipDef.GetResolvedFrames();
            float fps = baseClipDef.GetResolvedFps();
            if (frames == null || frames.Length == 0 || fps <= 0f)
                return false;

            outResolvedFrameCount = frames.Length;
            outFps = fps;

            int start = 0;
            int end = frames.Length;

            // Resolve slice range
            switch (slice.mode)
            {
                case MoveDef.VisualSlice.SliceMode.None:
                    start = 0;
                    end = frames.Length;
                    break;

                case MoveDef.VisualSlice.SliceMode.TimeMs:
                    {
                        int startMs = Mathf.Max(0, slice.startMs);
                        int endMs = slice.endMs > 0 ? slice.endMs : (startMs + Mathf.Max(0, phaseDurationMs));

                        // Convert ms -> frame indices using fps
                        start = Mathf.FloorToInt((startMs / 1000f) * fps);
                        end = Mathf.FloorToInt((endMs / 1000f) * fps);
                        break;
                    }

                case MoveDef.VisualSlice.SliceMode.FrameIndex:
                    {
                        start = Mathf.Max(0, slice.startFrame);

                        if (slice.endFrame > 0)
                        {
                            end = slice.endFrame;
                        }
                        else
                        {
                            // Derive from duration
                            int framesForDuration = Mathf.Max(1, Mathf.RoundToInt((Mathf.Max(0, phaseDurationMs) / 1000f) * fps));
                            end = start + framesForDuration;
                        }
                        break;
                    }
            }

            start = Mathf.Clamp(start, 0, frames.Length - 1);
            end = Mathf.Clamp(end, start + 1, frames.Length);

            int len = end - start;
            var sliced = new Sprite[len];
            System.Array.Copy(frames, start, sliced, 0, len);

            // Make the clip identity stable but unique per slice
            int baseId = baseClipDef.GetStableId();
            int sliceHash = (start * 397) ^ (end * 7919);
            int id = baseId ^ sliceHash;

            bool loop = slice.loopWithinSlice;
            bool restart = slice.restartOnEnter;

            outClip = new SpriteAnimationPlayer.ClipData(
                id: id,
                frames: sliced,
                fps: fps,
                loop: loop,
                restartOnEnter: restart
            );

            outDebugName = $"{baseClipDef.name}[{start}..{end})";
            outSliceStart = start;
            outSliceEnd = end;

            return true;
        }
    }
}
