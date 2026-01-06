using UnityEngine;

namespace Inspection
{
    /// <summary>
    /// Default inspectable implementation for props / objects.
    ///
    /// This component is intentionally data-centric:
    /// - Holds designer-authored display strings + icon
    /// - Provides basic vs detailed UI payload building
    /// - Provides optional hooks for inspection side effects
    /// </summary>
    public class InspectableComponent : MonoBehaviour, IInspectable
    {
        // In InspectableComponent.cs
        [Header("Hover Prompt")]
        [SerializeField] private string _hoverAction = "Inspect";      // Talk / Interact / Inspect
        [SerializeField] private string _hoverInputHint = "LMB";        // LMB / [E]
        [SerializeField] private string _hoverBlockedText = "Too far";  // optional override

        public string DisplayName => _displayName;
        public string HoverAction => _hoverAction;
        public string HoverInputHint => _hoverInputHint;
        public string HoverBlockedText => _hoverBlockedText;


        [Header("Inspection Display")]
        [SerializeField] private string _displayName = "Unnamed Object";

        [TextArea(1, 3)]
        [SerializeField] private string _shortDescription;

        [TextArea(3, 6)]
        [SerializeField] private string _longDescription;

        [SerializeField] private Sprite _icon;

        protected virtual void OnEnable()
        {
            Core.Registry.Register<InspectableComponent>(this);
            Core.Registry.Register<IInspectable>(this);
        }

        protected virtual void OnDisable()
        {
            Core.Registry.Unregister<InspectableComponent>(this);
            Core.Registry.Unregister<IInspectable>(this);
        }

        /// <summary>
        /// Detailed inspection payload (locked/selected).
        /// </summary>
        public virtual void BuildInspectionData(InspectionContext context, InspectionData data)
        {
            data.TargetRoot = gameObject;
            data.DisplayName = _displayName;
            data.ShortDescription = _shortDescription;
            data.LongDescription = _longDescription;
            data.Icon = _icon;
        }

        /// <summary>
        /// Basic inspection payload (hover/preview).
        /// Default behavior: omit long description to keep the panel lightweight.
        /// </summary>
        public virtual void BuildBasicInspectionData(InspectionContext context, InspectionData data)
        {
            data.TargetRoot = gameObject;
            data.DisplayName = _displayName;
            data.ShortDescription = _shortDescription;

            // Intentionally minimal for hover; UI can treat empty long-description as "preview".
            data.LongDescription = string.Empty;
            data.Icon = _icon;
        }

        /// <summary>
        /// Detailed inspection side effects (quests, clue flags, etc.).
        /// Default: no-op (kept quiet by default).
        /// </summary>
        public virtual void OnInspected(InspectionContext context)
        {
            // Override in specialized inspectables if/when you need real side effects.
            // Keeping base silent avoids spam when targeting changes.
        }

        /// <summary>
        /// Basic inspection side effects (hover-only).
        /// Default: no-op.
        /// </summary>
        public virtual void OnBasicInspected(InspectionContext context)
        {
            // Override for hover-only effects if you ever need them.
        }
    }
}
