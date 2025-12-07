using UnityEngine;

namespace Targeting
{
    /// <summary>
    /// A resolved target selection:
    /// - LogicalTarget: the entity/thing being targeted (ITargetable).
    /// - Anchor: the specific TargetComponent / subobject that was pointed at.
    /// </summary>
    public sealed class FocusTarget
    {
        /// <summary>
        /// Logical root target (what systems should act on).
        /// </summary>
        public ITargetable LogicalTarget { get; }

        /// <summary>
        /// Specific anchor that was used (subcomponent / child).
        /// </summary>
        public TargetableComponent Anchor { get; }

        /// <summary>
        /// Label of the logical target (for convenience).
        /// </summary>
        public string TargetLabel => LogicalTarget?.TargetLabel;

        /// <summary>
        /// The targeting agent (player, AI, etc.) that produced this focus.
        /// </summary>
        public ITargeter Targeter { get; }

        /// <summary>
        /// Distance from the player center (or reference point) when selected.
        /// </summary>
        public float Distance { get; }

        /// <summary>
        /// World position of the anchor at the time of selection.
        /// </summary>
        public Vector3 WorldPosition { get; }

        public FocusTarget(
            ITargeter targeter,
            ITargetable logicalTarget,
            TargetableComponent anchor,
            float distance,
            Vector3 worldPosition)
        {
            Targeter = targeter;
            LogicalTarget = logicalTarget;
            Anchor = anchor;
            Distance = distance;
            WorldPosition = worldPosition;
        }

        public override string ToString()
            => $"{TargetLabel} (dist={Distance:0.00})";
    }
}
