using System;
using System.Collections.Generic;
using UnityEngine;

namespace Inspection
{
    /// <summary>
    /// InspectorComponent
    ///
    /// Physics-free object inspection system.
    /// Matches the PlayerInteractor-style pattern:
    ///  - Continuous hover detection via ray
    ///  - Hover debug info in the inspector
    ///  - Clean gating + single-point inspection action
    ///  - Registry-driven candidate lookup
    ///  - Bounds-based ray testing (no Physics.Raycast)
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

        [Header("Ray Settings (non-physics)")]
        [SerializeField, Tooltip("Camera used for inspection rays. If null, Reset() assigns Camera.main.")]
        private Camera _camera;

        [SerializeField, Tooltip("If true, build the ray from the mouse position; otherwise from camera forward.")]
        private bool _useMousePosition = true;

        [SerializeField, Tooltip("Maximum distance along the ray to consider inspectable targets.")]
        private float _maxDistance = 20f;

        [Header("Debug")]
        [Tooltip("Print detailed logs for inspection events. Hover is always silent.")]
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
        [SerializeField, Tooltip("Current inspectable under the ray, if any.")]
        private InspectableComponent _hoverTarget;

        [SerializeField, Tooltip("Name of the current hover target.")]
        private string _hoverTargetName = "<none>";

        [SerializeField, Tooltip("Distance from camera to the hover target.")]
        private float _hoverDistance = 0f;

        [SerializeField, Tooltip("Is there a valid hover target right now?")]
        private bool _hasHoverTarget = false;

        [SerializeField, Tooltip("Would an inspection on the hover target succeed now?")]
        private bool _canInspectHover = false;

        [SerializeField, Tooltip("If inspection on hover is blocked, this explains why.")]
        private string _hoverBlockReason = string.Empty;

        [SerializeField, Tooltip("Ray parameter t for the hover hit (ray.origin + ray.direction * t).")]
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

        /// <summary>
        /// Source of inspectable candidates. By default uses the global registry,
        /// but subclasses can override for zone-specific logic.
        /// </summary>
        protected virtual IReadOnlyList<InspectableComponent> GetCandidates()
        {
            return InspectableRegistry.All;
        }

        // =====================================================================
        // UNITY
        // =====================================================================

        private void Reset()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
            }
        }

        private void Update()
        {
            if (_camera == null)
                return;

            // 1) Build ray every frame
            Ray ray = BuildRay();

            // 2) Update hover debug state
            UpdateHover(ray);

            // 3) On key press, attempt inspection using current hover target
            if (Input.GetKeyDown(_inspectKey))
            {
                TryInspectWithHover(ray);
            }
        }

        // =====================================================================
        // RAY CONSTRUCTION
        // =====================================================================

        private Ray BuildRay()
        {
            if (_useMousePosition)
            {
                return _camera.ScreenPointToRay(Input.mousePosition);
            }

            return new Ray(_camera.transform.position, _camera.transform.forward);
        }

        // =====================================================================
        // HOVER UPDATE
        // =====================================================================

        private void UpdateHover(in Ray ray)
        {
            if (!TryPickByRay(ray, _maxDistance, out var best, out float t))
            {
                _hoverTarget = null;
                _hoverTargetName = "<none>";
                _hoverDistance = 0f;
                _hoverRayT = 0f;
                _hasHoverTarget = false;
                _canInspectHover = false;
                _hoverBlockReason = string.Empty;
                return;
            }

            _hoverTarget = best;
            _hoverTargetName = best.name;
            _hoverRayT = t;
            _hoverDistance = Vector3.Distance(_camera.transform.position, best.transform.position);
            _hasHoverTarget = true;

            // Silent gating for hover.
            bool ok = EvaluateCanInspect(best, out string reason, verboseLog: false);
            _canInspectHover = ok;
            _hoverBlockReason = ok ? string.Empty : reason;
        }

        // =====================================================================
        // PICKING (physics-free, registry-driven)
        // =====================================================================

        private bool TryPickByRay(
            in Ray ray,
            float maxDistance,
            out InspectableComponent best,
            out float bestT)
        {
            best = null;
            bestT = float.MaxValue;
            float bestPriority = float.NegativeInfinity;

            var candidates = GetCandidates();
            if (candidates == null || candidates.Count == 0)
                return false;

            float maxT = maxDistance > 0f ? maxDistance : float.MaxValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                var it = candidates[i];
                if (!it || !it.isActiveAndEnabled)
                    continue;

                if (!it.RayTest(ray, out float t))
                    continue;

                if (t > maxT)
                    continue;

                bool closer = t < bestT;
                bool tieAndHigherPriority = Mathf.Approximately(t, bestT) &&
                                            it.SelectionPriority > bestPriority;

                if (closer || tieAndHigherPriority)
                {
                    best = it;
                    bestT = t;
                    bestPriority = it.SelectionPriority;
                }
            }

            return best != null;
        }

        // =====================================================================
        // INSPECTION ACTION
        // =====================================================================

        private void TryInspectWithHover(in Ray ray)
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

            float t = _hoverRayT;
            Vector3 hitPoint = ray.origin + ray.direction * t;

            Inspect(inspectable, hitPoint);

            if (_enableVerboseDebug)
            {
                Debug.Log($"[Inspector] Inspected '{_hoverTargetName}' at {hitPoint}");
            }
        }

        // =====================================================================
        // GATING + PIPELINE (IInspector)
        // =====================================================================

        private bool EvaluateCanInspect(IInspectable target, out string reason, bool verboseLog)
        {
            reason = string.Empty;

            if (!(target is Component comp))
            {
                reason = "Target is not a Component.";
                if (verboseLog && _enableVerboseDebug)
                {
                    Debug.LogWarning("[Inspector] CanInspect: target is not a Component.");
                }
                return false;
            }

            float dist = Vector3.Distance(_camera.transform.position, comp.transform.position);
            if (dist > _maxDistance)
            {
                reason = "Target is too far away.";
                if (verboseLog && _enableVerboseDebug)
                {
                    Debug.LogWarning("[Inspector] CanInspect: target too far away.");
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

            // 🔔 Notify UI / listeners
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

            // 🔔 Notify UI / listeners that inspection is cleared
            InspectionCleared?.Invoke();
        }

        // =====================================================================
        // GIZMOS
        // =====================================================================

        private void OnDrawGizmosSelected()
        {
            if (_camera == null)
                return;

            Ray ray;

            if (_useMousePosition && Application.isPlaying)
            {
                ray = _camera.ScreenPointToRay(Input.mousePosition);
            }
            else
            {
                ray = new Ray(_camera.transform.position, _camera.transform.forward);
            }

            Gizmos.color = (_hasHoverTarget && _canInspectHover) ? Color.green : Color.red;
            Vector3 end = ray.origin + ray.direction * _maxDistance;
            Gizmos.DrawLine(ray.origin, end);
        }
    }
}
