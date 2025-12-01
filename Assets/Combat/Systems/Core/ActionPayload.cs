using UnityEngine;
using System;

namespace Combat
{
    /// <summary>
    /// Serializable payload describing a single combat action's
    /// intent + factual hit data.
    ///
    /// Executors build this and feed it into the resolver / pipeline.
    /// </summary>
    [Serializable]
    public struct ActionPayload
    {
        [Serializable]
        public struct Facts
        {
            [Header("Who/What")]
            public GameObject instigator; // actor / root who initiated the action
            public GameObject source;     // weapon/ability object (optional)
            public GameObject target;     // concrete hit target (optional; can be null when routing by IDamageable)

            [Header("Where/When")]
            public Vector3 hitPoint;
            public Vector3 hitNormal;
            public string region;
            public float time;

            [Header("Timeline Meta (optional)")]
            public uint actionInstanceId; // unique id from ActionTimelineController
            public ActionPhase phaseId;   // phase at the moment of impact (Startup/Active/Recovery/etc.)
        }

        [Serializable]
        public struct Intent
        {
            [Header("Base Damage")]
            public float baseDamage;
            public string damageType; // e.g. "Slash"

            [Header("Control Effects (author intent)")]
            public int stunMs;
            public int hitstopFrames;

            [Header("Knockback Intent (NOT fully implemented yet)")]
            public float knockbackPower;
            public ImpulseDirMode impulseDirectionMode;
            public Vector3 authorDirection;

            public enum ImpulseDirMode
            {
                HitNormal,
                AuthorDirection,
                Tangent,
                Custom
            }

            [Header("Tags / Metadata")]
            public string[] tags;
            // Future: armorPenetration, poiseDamage, ignoresGuard, etc.
        }

        /// <summary>
        /// Factual data about who/what/where/when this action hit.
        /// </summary>
        public Facts facts;

        /// <summary>
        /// Designer-authored intent (damage, stun, knockback, tags...).
        /// </summary>
        public Intent intent;
    }
}
