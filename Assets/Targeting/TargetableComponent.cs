using System.Collections.Generic;
using UnityEngine;

namespace Targeting
{
    /// <summary>
    /// Target anchor + logical target container.
    /// 
    /// - Attach to an entity root (leave parentTarget null) to define a logical target.
    /// - Attach to child objects as additional anchors, with parentTarget set
    ///   to the root TargetComponent so they all represent the same logical target.
    /// 
    /// All instances participate in a static anchor list used by the targeting system.
    /// </summary>
    public class TargetableComponent : MonoBehaviour, ITargetable
    {
        // ---------- Static anchor list ---------- //

        private static readonly List<TargetableComponent> _allAnchors = new List<TargetableComponent>();
        public static IReadOnlyList<TargetableComponent> AllAnchors => _allAnchors;

        // ---------- Config ---------- //

        [Tooltip("Optional label for this logical target. Falls back to root GameObject name if empty.")]
        [SerializeField] private string targetLabelOverride;

        [Tooltip("Offset from THIS anchor's transform for the aim point.")]
        [SerializeField] private Vector3 localOffset = Vector3.zero;

        [Tooltip("If this is a child anchor, set this to the root TargetComponent.")]
        [SerializeField] private TargetableComponent parentTarget;

        /// <summary>
        /// The logical root TargetComponent. If parentTarget is null, this instance is the root.
        /// </summary>
        public TargetableComponent LogicalRoot
            => parentTarget != null ? parentTarget.LogicalRoot : this;

        // ---------- ITargetable (logical target) ---------- //

        public string TargetLabel
        {
            get
            {
                var root = LogicalRoot;
                if (!string.IsNullOrEmpty(root.targetLabelOverride))
                    return root.targetLabelOverride;

                return root.gameObject.name;
            }
        }

        public Transform TargetTransform => LogicalRoot.transform;

        public Vector3 TargetPosition => LogicalRoot.transform.position;

        // ---------- Anchor-specific info ---------- //

        /// <summary>
        /// The specific GameObject / subcomponent we aimed at (this anchor).
        /// </summary>
        public GameObject AnchorGameObject => gameObject;

        /// <summary>
        /// World position of THIS anchor (used for hover/lock math).
        /// </summary>
        public Vector3 AnchorWorldPosition => transform.TransformPoint(localOffset);

        // ---------- Lifecycle ---------- //

        private void OnEnable()
        {
            if (!_allAnchors.Contains(this))
                _allAnchors.Add(this);
        }

        private void OnDisable()
        {
            _allAnchors.Remove(this);
        }
    }
}
