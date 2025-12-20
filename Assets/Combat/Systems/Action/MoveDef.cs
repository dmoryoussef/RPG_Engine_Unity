using System.Collections.Generic;
using UnityEngine;
using Animation;

namespace Combat
{
    /// <summary>
    /// MoveDef
    ///
    /// Authoring model (clean):
    /// - One DirectionalVisualSet per move (moveVisuals)
    /// - Each phase specifies ONLY the slice of that move visual to play (VisualSlice)
    ///
    /// Why:
    /// - Avoid duplicating clips per phase (startup/active/recovery).
    /// - Keeps visuals consistent across phases while allowing phase-based subranges.
    /// - Phases stay authoritative for timing (durationMs) and gameplay logic.
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Move Definition", fileName = "NewMoveDef")]
    public sealed class MoveDef : ScriptableObject
    {
        [Header("Identity")]
        public string moveName = "New Move";

        [Header("Move Visuals")]
        [Tooltip("One visual set for the entire move. Phases select a slice within this animation.")]
        public DirectionalVisualSet moveVisuals = new DirectionalVisualSet();

        [Header("Phases")]
        public List<Phase> phases = new List<Phase>();

        public enum HitPolicy
        {
            /// <summary>Target can be hit once per phase.</summary>
            OncePerPhase = 0,

            /// <summary>Target can be hit once for the entire action instance (across all phases).</summary>
            OncePerAction = 1,

            /// <summary>No built-in de-dupe; use rateLimitMs if desired.</summary>
            Unlimited = 2
        }

        /// <summary>
        /// Defines what portion of the move's visual should be played during a phase.
        ///
        /// Range semantics:
        /// - Inclusive start, exclusive end: [start, end)
        ///
        /// Authoring recommendation:
        /// - Use TimeMs for most cases (matches durationMs).
        /// - Use FrameIndex if you want exact frame boundaries independent of FPS.
        /// </summary>
        [System.Serializable]
        public struct VisualSlice
        {
            public enum SliceMode
            {
                None = 0,
                TimeMs = 1,
                FrameIndex = 2
            }

            [Tooltip("How this slice range is authored.")]
            public SliceMode mode;

            [Header("Time Slice (ms)")]
            [Tooltip("Inclusive start time (ms) in the resolved clip timeline.")]
            public int startMs;

            [Tooltip("Exclusive end time (ms). If 0, consumers may use startMs + durationMs.")]
            public int endMs;

            [Header("Frame Slice (indices)")]
            [Tooltip("Inclusive start frame index in resolved frames.")]
            public int startFrame;

            [Tooltip("Exclusive end frame index. If 0, consumers may derive from durationMs.")]
            public int endFrame;

            [Header("Playback")]
            [Tooltip("If true, loops within the slice range. Usually false for action phases.")]
            public bool loopWithinSlice;

            [Tooltip("If true, restarts the slice on entering this phase.")]
            public bool restartOnEnter;
        }

        [System.Serializable]
        public sealed class Phase
        {
            [Header("Timing")]
            public ActionPhase phaseId;
            public int durationMs = 100;

            [Header("Visual Slice")]
            [Tooltip("Which portion of MoveDef.moveVisuals to play during this phase.")]
            public VisualSlice visualSlice;

            [Header("Hit Policy")]
            public HitPolicy hitPolicy = HitPolicy.OncePerPhase;

            [Tooltip("If > 0, prevents hitting the same target again until this many ms have elapsed (in addition to hitPolicy).")]
            public int rateLimitMs = 0;

            [Header("Active-only sweeps")]
            public List<SweepSpec> sweeps = new List<SweepSpec>();

            [Header("Payload (usually applied during Active)")]
            public ActionPayload.Intent damage;

            [Header("Interrupts / Cancels")]
            public InterruptPolicy interruptPolicy = InterruptPolicy.None;

            [Tooltip("If interruptPolicy is Whitelist, only these ActionIds may interrupt.")]
            public string[] interruptWhitelistActionIds;

            public enum InterruptPolicy
            {
                None = 0,
                Any = 1,
                Whitelist = 2
            }
        }

        [System.Serializable]
        public sealed class SweepSpec
        {
            [Tooltip("Path from attacker root to TipStart (e.g. 'Weapon/TipStart').")]
            public string tipStartPath;

            [Tooltip("Path from attacker root to TipEnd (e.g. 'Weapon/TipEnd').")]
            public string tipEndPath;

            public float radius = 0.1f;

            public Vector3 localOffsetStart;
            public Vector3 localOffsetEnd;
        }
    }
}
