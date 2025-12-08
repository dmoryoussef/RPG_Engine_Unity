using System;
using UnityEngine;
using Targeting; // <-- TargeterComponent, FocusTarget, etc.
using Logging;

namespace Inspection
{
    /// <summary>
    /// InspectorComponent
    ///
    /// Physics-free object inspection system.
    ///
    /// NOW:
    /// - Selection/hover comes from TargeterComponent (no internal picking).
    /// - Subscribes to TargeterComponent.Model.HoverChanged.
    /// - Maps FocusTarget -> InspectableComponent (via logical target root).
    /// - Runs a simple inspection pipeline with no distance gating.
    ///
    /// UI is handled separately by InspectionPanelSpawner listening to:
    ///  - InspectionCompleted
    ///  - InspectionCleared
    /// </summary>
    public class InspectorComponent : MonoBehaviour, IInspector
    {
        // =====================================================================
        // CONFIGURATION
        // =====================================================================

        [Header("Input")]
        [SerializeField]
        private KeyCode _inspectKey = KeyCode.Mouse1;

        [Header("Targeting")]
        [SerializeField, Tooltip("TargeterComponent that provides the current hover/selection.")]
        private TargeterBase _targeter;

        [Header("Camera (optional, for debug distance only)")]
        [SerializeField, Tooltip("Camera used for debug distance visualization. If null, Reset() assigns Camera.main.")]
        private Camera _camera;

        [Header("Debug")]
        [Tooltip("Print detailed logs for inspection events.")]
        [SerializeField]
        private bool _enableVerboseDebug = false;

        /// <summary>
        /// Fired whenever an inspection succeeds and InspectionData has been filled.
        /// Used by UI (e.g. InspectionPanelSpawner).
        /// </summary>
        public event Action<InspectionData> InspectionCompleted;

        /// <summary>
        /// Fired whenever the active inspection display should be cleared
        /// (no valid target, blocked, etc.).
        /// </summary>
        public event Action InspectionCleared;

        // =====================================================================
        // HOVER DEBUG (Read-Only)
        // =====================================================================

        [Header("Hover Debug (Read-Only)")]
        [SerializeField, Tooltip("Current inspectable resolved from the Targeter hover, if any.")]
        private InspectableComponent _hoverTarget;

        [SerializeField, Tooltip("Name of the current hover target.")]
        private string _hoverTargetName = "<none>";

        [SerializeField, Tooltip("Distance from camera to the hover target (for debug only).")]
        private float _hoverDistance = 0f;

        [SerializeField, Tooltip("Is there a valid hover target right now?")]
        private bool _hasHoverTarget = false;

        [SerializeField, Tooltip("Would an inspection on the hover target succeed now?")]
        private bool _canInspectHover = false;

        [SerializeField, Tooltip("If inspection on hover is blocked, this explains why.")]
        private string _hoverBlockReason = string.Empty;

        // Ray t is no longer meaningful here, but we keep it as a debug approximation.
        [SerializeField, Tooltip("Approximate distance to the hover target (for legacy debug).")]
        private float _hoverRayT = 0f;

        // =====================================================================
        // INSPECTION DEBUG STATE
        // =====================================================================

        [Serializable]
        public class DebugState
        {
            [Tooltip("GameObject of the last successfully inspected target.")]
            public GameObject LastInspectedTarget;

            [Tooltip("Display name from the last InspectionData package.")]
            public string LastDisplayName;

            [Tooltip("Short description from the last InspectionData package.")]
            [TextArea(1, 3)]
            public string LastShortDescription;

            [Tooltip("Long description from the last InspectionData package.")]
            [TextArea(3, 6)]
            public string LastLongDescription;

            [Tooltip("Icon from the last InspectionData package.")]
            public Sprite LastIcon;
        }

        [Header("Inspection Debug State (updated at runtime)")]
        [SerializeField]
        private DebugState _debugState = new DebugState();

        // =====================================================================
        // INTERNALS
        // =====================================================================

        public GameObject Root => gameObject;

        private readonly InspectionData _buffer = new InspectionData();

        // =====================================================================
        // UNITY
        // =====================================================================

        private void Reset()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
            }

            if (_targeter == null)
            {
                _targeter = GetComponent<TargeterBase>();
                if (_targeter == null)
                    GameLog.LogWarning(this, "No targeter found on GameObject.");
            }
        }

        // =====================================================================
        // LIFECYCLE
        // =====================================================================

        private void Awake()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
            }

            ITargeter targeter = GetComponent<ITargeter>();
            if (_targeter == null)
            {
                if (targeter is TargeterBase tb)
                {
                    _targeter = tb;
                }
                else
                    Logging.GameLog.LogWarning(this, "No Targeter component found.");
            }
        }

        private void OnEnable()
        {
            Core.Registry.Unregister<InspectorComponent>(this);

            if (this is IInspector inspector)
                Core.Registry.Unregister<IInspector>(inspector);

            if (_targeter != null && _targeter.Model != null)
            {
                _targeter.Model.HoverChanged += OnTargeterHoverChanged;
            }
            else if (_enableVerboseDebug)
            {
                Debug.LogWarning("[Inspector] No TargeterComponent/Model found; " +
                                 "inspection hover will never resolve.");
            }
        }

        private void OnDisable()
        {
            if (_targeter != null && _targeter.Model != null)
            {
                _targeter.Model.HoverChanged -= OnTargeterHoverChanged;
            }
        }


        private void Update()
        {
            if (_targeter == null)
                return;

            // Just handle input – hover is kept up to date via Targeter events.
            if (Input.GetKeyDown(_inspectKey))
            {
                TryInspectWithHover();
            }
        }

        // =====================================================================
        // HOVER UPDATE (via TargeterComponent events)
        // =====================================================================

        private void OnTargeterHoverChanged(FocusChange change)
        {
            var hoverFocus = change.Current;

            if (hoverFocus == null || hoverFocus.LogicalTarget == null)
            {
                ClearHoverDebug();
                return;
            }

            // Map FocusTarget -> InspectableComponent
            Transform logicalRoot = hoverFocus.LogicalTarget.TargetTransform;
            var inspectable = logicalRoot.GetComponentInParent<InspectableComponent>();

            if (inspectable == null || !inspectable.isActiveAndEnabled)
            {
                ClearHoverDebug();
                return;
            }

            _hoverTarget = inspectable;
            _hoverTargetName = inspectable.name;

            if (_camera != null)
            {
                Vector3 camPos = _camera.transform.position;
                Vector3 targetPos = inspectable.transform.position;
                _hoverDistance = Vector3.Distance(camPos, targetPos);
                _hoverRayT = _hoverDistance; // kept as an approximate legacy value
            }
            else
            {
                _hoverDistance = 0f;
                _hoverRayT = 0f;
            }

            _hasHoverTarget = true;

            // Simple gating for now (no distance requirement)
            bool ok = EvaluateCanInspect(inspectable, out string reason, verboseLog: false);
            _canInspectHover = ok;
            _hoverBlockReason = ok ? string.Empty : reason;
        }

        private void ClearHoverDebug()
        {
            _hoverTarget = null;
            _hoverTargetName = "<none>";
            _hoverDistance = 0f;
            _hoverRayT = 0f;
            _hasHoverTarget = false;
            _canInspectHover = false;
            _hoverBlockReason = string.Empty;
        }

        // =====================================================================
        // INSPECTION ACTION
        // =====================================================================

        private void TryInspectWithHover()
        {
            if (!_hasHoverTarget || _hoverTarget == null)
            {
                if (_enableVerboseDebug)
                {
                    Debug.Log("[Inspector] Inspect pressed but no hover target.");
                }

                ClearDisplay();
                return;
            }

            var inspectable = _hoverTarget;

            if (!CanInspect(inspectable, out string reason))
            {
                if (_enableVerboseDebug)
                {
                    Debug.Log($"[Inspector] Inspection blocked: {reason}");
                }

                ClearDisplay();
                return;
            }

            // Prefer Targeter’s hover world position if available,
            // otherwise fall back to the inspectable transform position.
            Vector3 hitPoint = inspectable.transform.position;
            var hoverFocus = _targeter.Model.Hover;
            if (hoverFocus != null)
            {
                hitPoint = hoverFocus.WorldPosition;
            }

            Inspect(inspectable, hitPoint);

            if (_enableVerboseDebug)
            {
                Debug.Log($"[Inspector] Inspected '{(_hoverTargetName ?? "<null>")}' at {hitPoint}");
            }
        }

        // =====================================================================
        // GATING + PIPELINE (IInspector)
        // =====================================================================

        private bool EvaluateCanInspect(IInspectable target, out string reason, bool verboseLog)
        {
            reason = string.Empty;

            if (!(target is Component))
            {
                reason = "Target is not a Component.";
                if (verboseLog && _enableVerboseDebug)
                {
                    Debug.LogWarning("[Inspector] CanInspect: target is not a Component.");
                }
                return false;
            }

            if (verboseLog && _enableVerboseDebug)
            {
                Debug.Log("[Inspector] CanInspect: OK.");
            }

            return true;
        }

        public bool CanInspect(IInspectable target, out string reason)
        {
            return EvaluateCanInspect(target, out reason, verboseLog: true);
        }

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

        public void Inspect(IInspectable target, Vector3 hitPoint)
        {
            if (!CanInspect(target, out string reason))
            {
                if (_enableVerboseDebug)
                {
                    Debug.Log($"[Inspector] Inspect aborted: {reason}");
                }

                ClearDisplay();
                return;
            }

            var context = BuildContext(target, hitPoint);

            _buffer.Clear();
            target.BuildInspectionData(context, _buffer);
            target.OnInspected(context, _buffer);

            _debugState.LastInspectedTarget = _buffer.TargetRoot;
            _debugState.LastDisplayName = _buffer.DisplayName;
            _debugState.LastShortDescription = _buffer.ShortDescription;
            _debugState.LastLongDescription = _buffer.LongDescription;
            _debugState.LastIcon = _buffer.Icon;

            // Notify UI / listeners
            InspectionCompleted?.Invoke(_buffer);
        }

        // =====================================================================
        // CLEAR DISPLAY
        // =====================================================================

        private void ClearDisplay()
        {
            _debugState.LastInspectedTarget = null;
            _debugState.LastDisplayName = string.Empty;
            _debugState.LastShortDescription = string.Empty;
            _debugState.LastLongDescription = string.Empty;
            _debugState.LastIcon = null;

            // Notify UI / listeners that inspection is cleared
            InspectionCleared?.Invoke();
        }
    }
}
