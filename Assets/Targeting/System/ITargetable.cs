using UnityEngine;

namespace Targeting
{
    /// <summary>
    /// Logical target capability.
    /// Represents the high-level thing being targeted (usually an entity).
    /// </summary>
    public interface ITargetable
    {
        /// <summary>
        /// Human-readable label for UI / logs. Not guaranteed unique.
        /// </summary>
        string TargetLabel { get; }

        /// <summary>
        /// Main transform for this logical target (usually the entity root).
        /// </summary>
        Transform TargetTransform { get; }

        /// <summary>
        /// Representative world position of the logical target (e.g. root pos).
        /// </summary>
        Vector3 TargetPosition { get; }
    }
}
