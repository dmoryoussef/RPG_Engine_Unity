using System;
using UnityEngine;
using Targeting;
using Logging;

namespace Inspection
{
    /// <summary>
    /// InspectorComponent
    ///
    /// Targeter-driven inspection system:
    /// - HoverChanged  -> Basic inspection (preview) => BasicInspectionCompleted
    /// - CurrentTargetChanged -> Detailed inspection (locked/selected) => InspectionCompleted
    ///
    /// UI is handled separately by listeners:
    ///  - Basic hover UI listens to BasicInspectionCompleted
    ///  - Main/locked UI listens to InspectionCompleted + InspectionCleared
    /// </summary>
    public class InspectorComponent : MonoBehaviour, IInspector
    {
        [Header("Targeting")]
        [SerializeField, Tooltip("Targeter that provides hover/current target events.")]
        private TargeterBase _targeter;

        [Header("Debug")]
        [SerializeField, Tooltip("Print detailed logs for inspection events.")]
        private bool _enableVerboseDebug = false;

        /// <summary>
        /// Fired whenever a detailed/locked inspection succeeds and InspectionData has been filled.
        /// Used by main UI panel spawners.
        /// </summary>
        public event Action<InspectionData> InspectionCompleted;

        /// <summary>
        /// Fired whenever a basic/hover inspection succeeds and InspectionData has been filled.
        /// Used by hover UI (tooltip/preview) spawners.
        /// </summary>
        public event Action<InspectionData> BasicInspectionCompleted;

        /// <summary>
        /// Fired when the main (detailed) inspection display should be cleared.
        /// </summary>
        public event Action InspectionCleared;

        [Serializable]
        public class DebugState
        {
            public GameObject LastInspectedTarget;
            public string LastDisplayName;

            [TextArea(1, 3)]
            public string LastShortDescription;

            [TextArea(3, 6)]
            public string LastLongDescription;

            public Sprite LastIcon;
        }

        [Header("Inspection Debug State (runtime)")]
        [SerializeField]
        private DebugState _debugState = new DebugState();

        public GameObject Root => gameObject;

        // Reuse one buffer to avoid allocations.
        private readonly InspectionData _buffer = new InspectionData();

        private void Reset()
        {
            if (_targeter == null)
            {
                _targeter = GetComponent<TargeterBase>();
                if (_targeter == null)
                    GameLog.LogWarning(this, "No TargeterBase found on GameObject.");
            }
        }

        private void Awake()
        {
            if (_targeter == null)
            {
                var t = GetComponent<ITargeter>();
                if (t is TargeterBase tb) _targeter = tb;
                else GameLog.LogWarning(this, "No Targeter component found.");
            }
        }

        private void OnEnable()
        {
            Core.Registry.Register<InspectorComponent>(this);
            if (this is IInspector inspector)
                Core.Registry.Register<IInspector>(inspector);

            if (_targeter?.Model == null)
            {
                if (_enableVerboseDebug)
                    Debug.LogWarning("[Inspector] No Targeter/Model found; inspection will not resolve.");
                return;
            }

            _targeter.Model.HoverChanged += OnHoverChanged;
            _targeter.Model.LockedChanged += OnLockedTargetChanged;
        }

        private void OnDisable()
        {
            Core.Registry.Unregister<InspectorComponent>(this);
            if (this is IInspector inspector)
                Core.Registry.Unregister<IInspector>(inspector);

            if (_targeter?.Model == null) return;

            _targeter.Model.HoverChanged -= OnHoverChanged;
            _targeter.Model.LockedChanged -= OnLockedTargetChanged;
        }

        // ---------------------------------------------------------------------
        // Targeter event handlers
        // ---------------------------------------------------------------------

        private void OnHoverChanged(FocusChange change)
        {
            var focus = change.Current;

            if (!TryResolveInspectable(focus, out var inspectable, out var hitPoint))
            {
                // NOTE: We intentionally do NOT clear the main panel here.
                // Hover UI can decide how to hide itself (or you can add BasicInspectionCleared later if desired).
                return;
            }

            InspectBasic(inspectable, hitPoint);
        }

        private void OnLockedTargetChanged(FocusChange target)
        {
            var locked = target.Current;
            if (!TryResolveInspectable(locked, out var inspectable, out var hitPoint))
            {
                // Losing current/locked target should close the main panel.
                ClearDisplay();
                return;
            }

            InspectDetailed(inspectable, hitPoint);
        }

        private bool TryResolveInspectable(FocusTarget focus, out IInspectable inspectable, out Vector3 hitPoint)
        {
            inspectable = null;
            hitPoint = default;

            var logicalRoot = focus?.LogicalTarget?.TargetTransform;
            if (logicalRoot == null) return false;

            var inspectableComp = logicalRoot.GetComponentInParent<InspectableComponent>();
            if (inspectableComp == null || !inspectableComp.isActiveAndEnabled) return false;

            inspectable = inspectableComp;
            hitPoint = focus.WorldPosition;
            return true;
        }

        // ---------------------------------------------------------------------
        // Context + inspection pipelines
        // ---------------------------------------------------------------------

        public InspectionContext BuildContext(IInspectable target, Vector3 hitPoint)
        {
            var comp = (Component)target;

            return new InspectionContext
            {
                InspectorRoot = Root,
                TargetRoot = comp.gameObject,
                WorldHitPoint = hitPoint
            };
        }

        public void InspectBasic(IInspectable target, Vector3 hitPoint)
        {
            if (!(target is Component)) return;

            var context = BuildContext(target, hitPoint);

            _buffer.Clear();
            target.BuildBasicInspectionData(context, _buffer);
            target.OnBasicInspected(context);

            // Debug snapshot (optional: keeps inspector inspector window useful)
            DebugDisplay(_buffer);

            BasicInspectionCompleted?.Invoke(_buffer);

            if (_enableVerboseDebug)
                Debug.Log($"[Inspector] BASIC: '{_buffer.DisplayName}'");
        }

        public void InspectDetailed(IInspectable target, Vector3 hitPoint)
        {
            if (!(target is Component))
            {
                ClearDisplay();
                return;
            }

            var context = BuildContext(target, hitPoint);

            _buffer.Clear();
            target.BuildInspectionData(context, _buffer);
            target.OnInspected(context);

            DebugDisplay(_buffer);

            InspectionCompleted?.Invoke(_buffer);

            if (_enableVerboseDebug)
                Debug.Log($"[Inspector] DETAILED: '{_buffer.DisplayName}'");
        }

        private void DebugDisplay(InspectionData data)
        {
            _debugState.LastInspectedTarget = data.TargetRoot;
            _debugState.LastDisplayName = data.DisplayName;
            _debugState.LastShortDescription = data.ShortDescription;
            _debugState.LastLongDescription = data.LongDescription;
            _debugState.LastIcon = data.Icon;
        }

        private void ClearDisplay()
        {
            _debugState.LastInspectedTarget = null;
            _debugState.LastDisplayName = string.Empty;
            _debugState.LastShortDescription = string.Empty;
            _debugState.LastLongDescription = string.Empty;
            _debugState.LastIcon = null;

            InspectionCleared?.Invoke();

            if (_enableVerboseDebug)
                Debug.Log("[Inspector] Cleared detailed display.");
        }
    }
}
