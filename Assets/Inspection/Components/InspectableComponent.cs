using Interaction;
using UnityEngine;

namespace Inspection
{
    /// <summary>
    /// Default inspectable implementation for props / objects.
    ///
    /// Designers can:
    /// - Drop this on a GameObject
    /// - Fill in the display fields
    /// - Optionally tweak the bounds / selection priority
    ///
    /// It provides:
    /// - Basic text + icon data for inspection
    /// - No special behavior on inspection (subclasses can override)
    /// - Registration with InspectableRegistry for physics-free picking
    /// </summary>
    public class InspectableComponent : MonoBehaviour, IInspectable
    {
        // --------------------------------------------------------------------
        // Display data
        // --------------------------------------------------------------------

        [Header("Inspection Display")]
        [SerializeField] private string _displayName = "Unnamed Object";

        [TextArea(1, 3)]
        [SerializeField] private string _shortDescription;

        [TextArea(3, 6)]
        [SerializeField] private string _longDescription;

        [SerializeField] private Sprite _icon;

        // --------------------------------------------------------------------
        // Selection / Bounds (physics-free picking)
        // --------------------------------------------------------------------

        [Header("Selection")]
        [Tooltip("Higher wins when multiple inspectables overlap the same ray.")]
        [SerializeField] private float _selectionPriority = 0f;
        public float SelectionPriority => _selectionPriority;

        [Header("Bounds Source")]
        [Tooltip(
            "If set, use this renderer's bounds for ray hit-testing.\n" +
            "If null, auto-pick the first Renderer on this object or its children.\n" +
            "If still null, falls back to a small box around the transform.")]
        [SerializeField] private Renderer _boundsRenderer;

        [Tooltip("Optional manual local-space center offset for bounds.")]
        [SerializeField] private Vector3 _manualCenter = Vector3.zero;

        [Tooltip("Optional manual local-space size for bounds. Leave zero to ignore.")]
        [SerializeField] private Vector3 _manualSize = Vector3.zero;

        // --------------------------------------------------------------------
        // Lifecycle: registry
        // --------------------------------------------------------------------

        protected virtual void OnEnable()
        {
            World.Registry.Register<InspectableComponent>(this);

            if (this is IInspectable inspectable)
                World.Registry.Register<IInspectable>(inspectable);
        }

        protected virtual void OnDisable()
        {
            World.Registry.Unregister<InspectableComponent>(this);

            if (this is IInspectable inspectable)
                World.Registry.Unregister<IInspectable>(inspectable);
        }

        // --------------------------------------------------------------------
        // IInspectable
        // --------------------------------------------------------------------

        /// <summary>
        /// Fill out the inspection data with this object's configured info.
        /// </summary>
        public virtual void BuildInspectionData(InspectionContext context, InspectionData data)
        {
            //Debug.Log($"[InspectableComponent] BuildInspectionData called on '{name}'. " +
            //          $"Inspector='{context.InspectorRoot?.name}', TargetRoot='{context.TargetRoot?.name}'");

            data.TargetRoot = gameObject;
            data.DisplayName = _displayName;
            data.ShortDescription = _shortDescription;
            data.LongDescription = _longDescription;
            data.Icon = _icon;
        }

        /// <summary>
        /// Called whenever an inspector successfully inspects this object.
        ///
        /// Default implementation logs only. Override to:
        /// - Mark clues discovered
        /// - Trigger story events
        /// - Record journal entries
        /// </summary>
        public virtual void OnInspected(InspectionContext context, InspectionData data)
        {
            Debug.Log($"[InspectableComponent] OnInspected called on '{name}'. DisplayName='{data.DisplayName}'");
            // Custom behavior in subclasses; base does nothing beyond log.
        }

        // --------------------------------------------------------------------
        // Physics-free picking helpers (similar to Interaction.InteractableBase)
        // --------------------------------------------------------------------

        /// <summary>
        /// Default ray hit-test: Ray vs world-space AABB using Unity's Bounds.IntersectRay.
        /// Override for custom shapes (sprite masks, custom colliders, etc.) if needed.
        /// </summary>
        public virtual bool RayTest(in Ray ray, out float distance)
        {
            var b = GetWorldBounds();
            return b.IntersectRay(ray, out distance);
        }

        /// <summary>
        /// Returns the world-space bounding box used for selection.
        /// - Manual size override, if provided
        /// - Otherwise first Renderer bounds
        /// - Otherwise a small box around the transform
        /// </summary>
        public Bounds GetWorldBounds()
        {
            // Manual override?
            if (_manualSize != Vector3.zero)
            {
                var worldCenter = transform.TransformPoint(_manualCenter);
                var scaled = Vector3.Scale(_manualSize, Abs(transform.lossyScale));
                var b = new Bounds(worldCenter, scaled);
                EnsureMinThickness(ref b);
                return b;
            }

            // Renderer?
            var r = _boundsRenderer != null ? _boundsRenderer : GetComponentInChildren<Renderer>();
            if (r != null)
            {
                var b = r.bounds;
                EnsureMinThickness(ref b);
                return b;
            }

            // Fallback small box
            var fb = new Bounds(transform.position, new Vector3(0.5f, 0.5f, 0.2f));
            return fb;
        }

        private static Vector3 Abs(Vector3 v)
        {
            return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        }

        private static void EnsureMinThickness(ref Bounds b)
        {
            const float minZ = 0.05f;
            var s = b.size;
            if (s.z < minZ)
            {
                s.z = minZ;
                b.size = s;
            }
        }
    }
}
