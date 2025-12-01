// DamageCore.cs
// Purpose: Basic damage-related enums + marker interface for combat targets.

using UnityEngine;

namespace Combat
{
    public enum DamageType
    {
        Blunt,
        Slash,
        Pierce,
        Fire,
        Cold,
        Electric,
        Acid,
        Magic,
        True
    }

    /// <summary>
    /// Marker interface: "this GameObject is a valid combat action target node".
    ///
    /// IMPORTANT:
    ///  - This no longer contains any effect methods (no SubtractHealth, ApplyStun, etc.).
    ///  - It is intentionally thin. Effects are handled by separate receiver components.
    ///  - Implement this on a simple MonoBehaviour (e.g., ActionTargetRoot).
    /// </summary>
    public interface IActionTarget
    {
        // No members on purpose. Marker only.
        // We rely on MonoBehaviour to get the GameObject when needed.
    }
}
