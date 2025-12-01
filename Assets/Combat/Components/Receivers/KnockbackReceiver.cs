// KnockbackReceiver.cs
// Purpose: Only handles knockback impulses for a combat target node,
//          with sane defaults and safety clamps so hits don't yeet
//          things into orbit by accident.

using UnityEngine;

namespace Combat
{
    /// <summary>
    /// Applies knockback impulses to a Rigidbody on this GameObject.
    ///
    /// Notes for future you:
    /// - This component NEVER triggers on its own. It only moves the body
    ///   when the combat pipeline calls ApplyKnockback().
    /// - If you're seeing movement "on play", that means some action is
    ///   successfully hitting this target very early, and the resolved
    ///   knockback magnitude is > 0.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class KnockbackReceiver : MonoBehaviour, IKnockbackReceiver
    {
        [Header("Knockback Settings")]
        [Tooltip("Scale applied to the resolved knockback magnitude from the combat system.")]
        [SerializeField] private float _magnitudeScale = 1f;

        [Tooltip("Minimum knockback magnitude required before we apply any force at all.")]
        [SerializeField] private float _minMagnitude = 0.1f;

        [Tooltip("Optional clamp on the resulting velocity change (0 = no clamp).")]
        [SerializeField] private float _maxVelocityChange = 0f;

        [Tooltip("Force mode used for knockback. Impulse is usually safer than VelocityChange.")]
        [SerializeField] private ForceMode _forceMode = ForceMode.Impulse;

        [Header("Debug")]
        [SerializeField] private bool _logKnockback = true;

        private Rigidbody _rb;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        /// <summary>
        /// Called by the combat pipeline when this target should receive a knockback impulse.
        /// </summary>
        public void ApplyKnockback(Vector3 dir, float mag, ActionContext ctx)
        {
            Debug.Log("[KnockbackReceiver] ApplyKnockback.");
            if (_rb == null)
            {
                Debug.Log("[KnockbackReceiver] Error: No rigidbody.");
                return;
            }

            if (mag <= _minMagnitude)
            {
                // Ignore tiny "micro impulses" to avoid jitter and accidental nudges.
                if (_logKnockback)
                {
                    Debug.Log(
                        $"[KnockbackReceiver] Ignoring tiny knockback (mag={mag}) on {name}.",
                        this);
                }
                return;
            }

            // Normalize direction, but guard against zero-length vectors.
            if (dir.sqrMagnitude < 1e-6f)
            {
                if (_logKnockback)
                {
                    Debug.LogWarning(
                        $"[KnockbackReceiver] Received knockback with zero direction on {name}. Ignoring.",
                        this);
                }
                return;
            }

            Vector3 direction = dir.normalized;
            float scaledMag = mag * _magnitudeScale;

            // Convert magnitude to an impulse or velocity change depending on mode.
            Vector3 impulse = direction * scaledMag;

            if (_forceMode == ForceMode.VelocityChange && _maxVelocityChange > 0f)
            {
                // Clamp velocity change to avoid insane launch speeds.
                if (impulse.magnitude > _maxVelocityChange)
                    impulse = impulse.normalized * _maxVelocityChange;
            }

            _rb.AddForce(impulse, _forceMode);

            if (_logKnockback)
            {
                string attackerName = ctx.Attacker != null ? ctx.Attacker.name : "<unknown>";
                Debug.Log(
                    $"[KnockbackReceiver] {name} knockback mag={mag} scaled={scaledMag}, dir={direction}, mode={_forceMode}, attacker={attackerName}.",
                    this);
            }
        }
    }
}
