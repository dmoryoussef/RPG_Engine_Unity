using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    [CreateAssetMenu(menuName = "Combat/MoveDef", fileName = "NewMoveDef")]
    public class MoveDef : ScriptableObject
    {
        public string moveName = "New Move";
        public List<Phase> phases = new List<Phase>(); // Startup → Active → Recovery order

        [System.Serializable]
        public class Phase
        {
            public ActionPhase phaseId;
            public int durationMs = 100;

            [Header("Hit Policy")]
            public bool oneHitPerPhase = true;
            public int rateLimitMs = 0;

            [Header("Active-only sweeps")]
            public List<SweepSpec> sweeps = new List<SweepSpec>();

            [Header("Damage Template (applied during Active)")]
            public ActionPayload.Intent damage;
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
