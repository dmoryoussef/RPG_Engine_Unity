using System.Collections.Generic;
using UnityEngine;

namespace Interaction
{
    /// <summary>
    /// Standardized reasons why an interaction attempt might fail.
    /// Used by InteractableBase + InteractorBase for debug/UI.
    /// </summary>
    public enum InteractionFailReason
    {
        None = 0,

        // No valid target or selection available.
        NoTarget,

        // Global rules (cooldowns, uses, etc.).
        Cooldown,
        OutOfUses,

        // Environment / state gating.
        Locked,
        Blocked,
        AlreadyInDesiredState,
        OutOfRange,
        NotFacing,

        // Catch-all bucket when nothing more specific is set.
        Other
    }

    /// <summary>
    /// The minimal contract for anything the player can interact with.
    /// Keeps core responsibilities isolated from optional capabilities.
    ///
    /// NOTE:
    /// - For most game objects, inherit from InteractableBase instead of
    ///   implementing IInteractable directly. InteractableBase adds cooldowns,
    ///   max-uses, events, and debug helpers.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// Unique identifier used for debugging, saving, or UI display.
        /// </summary>
        string InteractableId { get; }

        KeyCode InteractionKey { get; }

        /// <summary>
        /// Called when the player interacts with this object.
        /// Returns true if the interaction succeeded.
        ///
        /// InteractableBase already implements this with cooldown / uses logic.
        /// Override DoInteract() in InteractableBase instead of this directly.
        /// </summary>
        bool OnInteract();
    }

    /// <summary>
    /// Optional interface: adds selection priority for overlap resolution.
    /// Higher values are preferred when multiple interactables are in range.
    /// (InteractableBase already exposes a SelectionPriority property.)
    /// </summary>
    public interface IInteractablePrioritized
    {
        float SelectionPriority { get; }
    }

    /// <summary>
    /// Optional interface: adds focus/selection highlighting hooks.
    /// InteractorBase can call these when the current target changes.
    /// </summary>
    public interface IInteractableFocusable
    {
        void OnFocusGained();
        void OnFocusLost();
    }

    /// <summary>
    /// Optional interface: adds state persistence support.
    /// </summary>
    public interface IInteractableState
    {
        object CaptureState();
        void ApplyState(object state);
    }

    /// <summary>
    /// Optional interface: provides debug / inspection data for UI overlays.
    /// </summary>
    public interface IInteractableInspectable
    {
        IEnumerable<(string key, string value)> Inspect();
    }
}
