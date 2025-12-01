// ActionCombatTypes.cs
// Purpose: Shared combat action types: ActionContext + receiver/reactor interfaces.

using UnityEngine;

namespace Combat
{
    /// <summary>
    /// Full context for a single resolved combat action on a target.
    ///
    /// Bundles:
    ///  - Attacker GameObject
    ///  - Target GameObject
    ///  - Original ActionPayload (facts + author intent)
    ///  - Resolved DamageResult (final numbers + effects)
    /// </summary>
    public readonly struct ActionContext
    {
        public GameObject Attacker { get; }
        public GameObject Target { get; }
        public ActionPayload Payload { get; }
        public DamageResult Result { get; }

        public ActionContext(GameObject attacker,
                             GameObject target,
                             in ActionPayload payload,
                             in DamageResult result)
        {
            Attacker = attacker;
            Target = target;
            Payload = payload;
            Result = result;
        }
    }

    /// <summary>
    /// Marker base for all components that receive or react to combat action effects.
    ///
    /// This helps with tooling ("list all effect handlers on this node"),
    /// but does NOT replace IActionTarget.
    /// </summary>
    public interface IActionEffectReceiver : IActionTarget { }

    // ─────────────────────────────────────────────────────────────────────
    // Core effect receivers (apply effects)
    // ─────────────────────────────────────────────────────────────────────

    public interface IHealthReceiver : IActionEffectReceiver
    {
        /// <summary>
        /// Apply a health delta. Negative = damage, positive = heal.
        /// </summary>
        void ApplyHealthChange(float amount, ActionContext ctx);
    }

    public interface IKnockbackReceiver : IActionEffectReceiver
    {
        /// <summary>
        /// Apply a knockback impulse in dir * mag.
        /// </summary>
        void ApplyKnockback(Vector3 dir, float mag, ActionContext ctx);
    }

    public interface IStunReceiver : IActionEffectReceiver
    {
        /// <summary>
        /// Apply a stun duration in milliseconds.
        /// </summary>
        void ApplyStun(int durationMs, ActionContext ctx);
    }

    public interface IHitstopReceiver : IActionEffectReceiver
    {
        /// <summary>
        /// Apply hitstop duration in frames.
        /// </summary>
        void ApplyHitstop(int frames, ActionContext ctx);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Reactors (respond after core effects)
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Target-side reactors: respond after this target has been affected by an action.
    /// Examples: thorns, curses, retaliation.
    /// </summary>
    public interface IOnActionTakenReactor : IActionEffectReceiver
    {
        void OnActionTaken(ActionContext ctx);
    }

    /// <summary>
    /// Attacker-side reactors: respond after this attacker has dealt an action.
    /// Examples: lifesteal, meter gain, self-buff-on-hit.
    /// </summary>
    public interface IOnActionDealtReactor : IActionEffectReceiver
    {
        void OnActionDealt(ActionContext ctx);
    }
}
