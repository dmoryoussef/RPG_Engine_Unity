using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    [CreateAssetMenu(menuName = "Combat/MoveDef", fileName = "NewMoveDef")]
    public class MoveDef : ScriptableObject
    {
        public string moveName = "New Move";
        public List<Phase> phases = new List<Phase>(); // Startup → Active → Recovery order
        public ActionSpriteAnimationDef animationDef;

        public enum HitPolicy
        {
            /// <summary>Target can be hit once per phase (classic "one-hit active window").</summary>
            OncePerPhase = 0,

            /// <summary>Target can be hit once for the entire action instance (across all phases).</summary>
            OncePerAction = 1,

            /// <summary>No built-in de-dupe. Use rateLimitMs if desired.</summary>
            Unlimited = 2
        }

        [System.Serializable]
        public class Phase
        {
            public ActionPhase phaseId;
            public int durationMs = 100;

            [Header("Hit Policy")]
            public HitPolicy hitPolicy = HitPolicy.OncePerPhase;

            [Tooltip("If > 0, prevents hitting the same target again until this many ms have elapsed (in addition to hitPolicy).")]
            public int rateLimitMs = 0;

            [Header("Active-only sweeps")]
            public List<SweepSpec> sweeps = new List<SweepSpec>();

            [Header("Damage Template (usually used during Active)")]
            public ActionPayload.Intent damage;

            [Header("Interrupts / Cancels")]
            public InterruptPolicy interruptPolicy = InterruptPolicy.None;

            [Tooltip("If interruptPolicy is Whitelist, only these ActionIds may interrupt.")]
            public string[] interruptWhitelistActionIds;

            public enum InterruptPolicy
            {
                None = 0,          // cannot be interrupted during this phase
                Any = 1,           // can be interrupted by any action
                Whitelist = 2      // can be interrupted only by listed actionIds
            }
        }

        [System.Serializable]
        public class SweepSpec
        {
            [Tooltip("Path from attacker root to TipStart (e.g. 'Weapon/TipStart').")]
            public string tipStartPath;

            [Tooltip("Path from attacker root to TipEnd (e.g., 'Weapon/TipEnd').")]
            public string tipEndPath;

            public float radius = 0.1f;

            public Vector3 localOffsetStart;
            public Vector3 localOffsetEnd;
        }
    }
}
