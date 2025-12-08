using System;
using UnityEngine;

namespace Targeting
{
    /// <summary>
    /// Target anchor + logical target container.
    /// 
    /// - Attach to an entity root (leave parentTarget null) to define a logical target.
    /// - Attach to child objects as additional anchors, with parentTarget set
    ///   to the root TargetableComponent so they all represent the same logical target.
    /// 
    /// All instances participate in the WorldRegistry and can be discovered
    /// as TargetableComponent (framework/tools) or ITargetable (gameplay).
    /// </summary>
    public class TargetableComponent : MonoBehaviour, ITargetable
    {
        // ---------- Config ---------- //

        [Tooltip("Optional label for this logical target. Falls back to root GameObject name if empty.")]
        [SerializeField] private string targetLabelOverride;

        [Tooltip("Offset from THIS anchor's transform for the aim point.")]
        [SerializeField] private Vector3 localOffset = Vector3.zero;

        [Tooltip("If this is a child anchor, set this to the root TargetableComponent.")]
        [SerializeField] private TargetableComponent parentTarget;

        [Header("Current targeter")]
        ITargeter _targeter;

        /// <summary>
        /// The logical root TargetableComponent. If parentTarget is null, this instance is the root.
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

        // ---------- Targeting events ---------- //

        /// <summary>
        /// Fired when this anchor becomes the current hover target.
        /// </summary>
        public event Action<TargetableComponent, FocusTarget> Hovered;

        /// <summary>
        /// Fired when this anchor stops being the current hover target.
        /// </summary>
        public event Action<TargetableComponent, FocusTarget> Unhovered;

        /// <summary>
        /// Fired when this anchor becomes the current locked/targeted anchor.
        /// </summary>
        public event Action<TargetableComponent, FocusTarget> Targeted;

        /// <summary>
        /// Fired when this anchor stops being the current locked/targeted anchor.
        /// </summary>
        public event Action<TargetableComponent, FocusTarget> Untargeted;

        // These are called by the TargeterComponent based on FocusChange events.
        internal void RaiseHovered(FocusTarget focus) => Hovered?.Invoke(this, focus);
        internal void RaiseUnhovered(FocusTarget focus) => Unhovered?.Invoke(this, focus);
        internal void RaiseTargeted(FocusTarget focus)
        {
            _targeter = focus.Targeter;
            Targeted?.Invoke(this, focus);
        }
        internal void RaiseUntargeted(FocusTarget focus) => Untargeted?.Invoke(this, focus);

        // ---------- Lifecycle / Registry membership ---------- //

        private void OnEnable()
        {
            // Framework / tooling view
            Core.Registry.Register<TargetableComponent>(this);

            // Gameplay / systems view (targeting, AI, etc.)
            Core.Registry.Register<ITargetable>(this);
        }

        private void OnDisable()
        {
            Core.Registry.Unregister<TargetableComponent>(this);
            Core.Registry.Unregister<ITargetable>(this);
        }
    }
}
