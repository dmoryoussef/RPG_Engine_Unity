using System.Collections.Generic;
using UnityEngine;

namespace Targeting
{
    /// <summary>
    /// Input-agnostic targeting core.
    /// 
    /// Responsibilities:
    /// - Owns TargetingContextModel
    /// - Performs hover picking via ray-sphere checks
    /// - Maintains lock-on via FOV + radius and cycling
    /// - Queries TargetableComponent anchors from WorldRegistry
    /// - Drives TargetableComponent hooks (Hovered/Unhovered/Targeted/Untargeted)
    /// - Exposes debug gizmos
    /// 
    /// DOES NOT:
    /// - Read input
    /// - Decide *when* to lock, cycle, or clear
    /// 
    /// Derived classes (player, AI, etc.) call the protected methods:
    /// - UpdateHoverFromRay(...)
    /// - LockFromHover()
    /// - CycleLockFromFov()
    /// - ClearLock()
    /// </summary>
    public abstract class TargeterBase : MonoBehaviour, ITargeter
    {
        // ====================================================================
        // Core model & references
        // ====================================================================

        [Header("Core References")]
        [SerializeField] protected Camera targetCamera;
        [SerializeField] protected Transform centerTransform;

        /// <summary>
        /// Core targeting model consumed by other systems (interaction, combat, etc).
        /// Single writer: this component.
        /// </summary>
        public TargetingContextModel Model { get; private set; } = new TargetingContextModel();

        // ====================================================================
        // Hover picking configuration
        // ====================================================================

        [Header("Hover Picking (Ray-Sphere)")]
        [Tooltip("Base world radius multiplier for hover picking spheres.")]
        [SerializeField] protected float hoverBaseWorldRadius = 0.1f;

        [Tooltip("Minimum world radius, even for very small targets.")]
        [SerializeField] protected float hoverMinWorldRadius = 0.05f;

        [Tooltip("Extra buffer added to the scaled radius for easier selection.")]
        [SerializeField] protected float hoverWorldRadiusBuffer = 0.05f;

        [Tooltip("Optional max distance along the hover ray. <= 0 means infinite.")]
        [SerializeField] protected float hoverMaxRayDistance = 0f;

        // ====================================================================
        // Lock-On configuration
        // ====================================================================

        [Header("Lock-On (FOV + Radius)")]
        [Tooltip("Maximum world-space distance for lock-on candidates.")]
        [SerializeField] protected float lockRadius = 5f;

        [Tooltip("Field-of-view angle (degrees) for lock-on.")]
        [SerializeField] protected float lockFovDegrees = 80f;

        // ====================================================================
        // Runtime FocusTargets (Inspector-visible)
        // ====================================================================

        [Header("Runtime Targets (Read-Only)")]
        [Tooltip("Current hover target.")]
        public FocusTarget CurrentHover;

        [Tooltip("Current locked target (lock-on).")]
        public FocusTarget CurrentLocked;

        [Tooltip("Current focus target (priority resolved).")]
        public FocusTarget CurrentFocus;

        [Header("Runtime Debug Labels")]
        [SerializeField] private string debugHoverLabel = "(null)";
        [SerializeField] private string debugLockedLabel = "(null)";
        [SerializeField] private string debugFocusLabel = "(null)";

        [Header("Debug Logging")]
        [SerializeField] private bool logCurrentTargetChanges = false;

        // ====================================================================
        // Debug Gizmos
        // ====================================================================

        [Header("Debug Gizmos")]
        [SerializeField] private bool _drawHoverGizmos = true;
        [SerializeField] private bool _drawLockGizmos = true;
        [SerializeField] private bool _drawAllAnchors = false;

        // Lock-on candidate cache (reused every frame).
        protected readonly List<TargetableComponent> _lockCandidates = new();

        // Registry query buffer for all TargetableComponent anchors.
        protected readonly List<TargetableComponent> _anchorsBuffer = new();

        // ====================================================================
        // UNITY LIFECYCLE
        // ====================================================================

        protected virtual void Awake()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;

            if (centerTransform == null)
                centerTransform = transform;

            Model.HoverChanged += OnHoverChanged;
            Model.LockedChanged += OnLockedChanged;
            Model.FocusChanged += OnFocusChanged;
        }

        protected virtual void OnDestroy()
        {
            if (Model == null) return;

            Model.HoverChanged -= OnHoverChanged;
            Model.LockedChanged -= OnLockedChanged;
            Model.FocusChanged -= OnFocusChanged;
        }

        /// <summary>
        /// Derived classes implement this and call:
        /// - UpdateHoverFromRay(...)
        /// - LockFromHover()
        /// - CycleLockFromFov()
        /// - ClearLock()
        /// as appropriate for their control scheme (player input, AI, etc.).
        /// </summary>
        protected abstract void TickTargeter();

        protected virtual void Update()
        {
            if (Model == null)
                return;

            TickTargeter();
        }

        // ====================================================================
        // Event handlers – sync inspector fields & call Targetable hooks
        // ====================================================================

        private void OnHoverChanged(FocusChange change)
        {
            // Call anchor hooks
            var prevAnchor = change.Previous?.Anchor;
            var newAnchor = change.Current?.Anchor;

            if (prevAnchor != null && prevAnchor != newAnchor)
                prevAnchor.RaiseUnhovered(change.Previous);

            if (newAnchor != null && newAnchor != prevAnchor)
                newAnchor.RaiseHovered(change.Current);

            // Mirror for debugging/inspection
            CurrentHover = change.Current;
            debugHoverLabel = change.Current?.TargetLabel ?? "(null)";
        }

        private void OnLockedChanged(FocusChange change)
        {
            var prevAnchor = change.Previous?.Anchor;
            var newAnchor = change.Current?.Anchor;

            if (prevAnchor != null && prevAnchor != newAnchor)
                prevAnchor.RaiseUntargeted(change.Previous);

            if (newAnchor != null && newAnchor != prevAnchor)
                newAnchor.RaiseTargeted(change.Current);

            CurrentLocked = change.Current;
            debugLockedLabel = change.Current?.TargetLabel ?? "(null)";
        }

        private void OnFocusChanged(FocusChange change)
        {
            CurrentFocus = change.Current;
            debugFocusLabel = change.Current?.TargetLabel ?? "(null)";
        }

        // ====================================================================
        // Core operations for derived classes
        // ====================================================================

        /// <summary>
        /// Let a derived class feed us a ray (player mouse, AI line-of-sight, etc.).
        /// </summary>
        protected void UpdateHoverFromRay(Ray ray)
        {
            if (TryPickTargetFromRay(ray, out var bestTarget))
                Model.SetHover(bestTarget);
            else
                Model.ClearHover();
        }

        /// <summary>
        /// Lock onto the current hover target, if any.
        /// </summary>
        protected void LockFromHover()
        {
            var hover = Model.Hover;
            if (hover == null)
                return;

            var logical = hover.LogicalTarget;
            var anchor = hover.Anchor;

            if (logical == null || anchor == null)
                return;

            Vector3 worldPos = anchor.AnchorWorldPosition;
            float dist = Vector3.Distance(centerTransform.position, worldPos);

            var locked = new FocusTarget(
                this,
                logical,
                anchor,
                dist,
                worldPos
            );

            Model.SetLocked(locked);
        }

        /// <summary>
        /// Cycle the lock-on target among all FoV candidates.
        /// </summary>
        protected void CycleLockFromFov()
        {
            _lockCandidates.Clear();
            FindLockCandidates(_lockCandidates);

            if (_lockCandidates.Count == 0)
            {
                Model.ClearLocked();
                return;
            }

            var currentLocked = Model.Locked;
            var nextAnchor = GetNextLockCandidateLinear(_lockCandidates, currentLocked);

            if (nextAnchor == null)
            {
                Model.ClearLocked();
                return;
            }

            var logical = (ITargetable)nextAnchor.LogicalRoot;

            Vector3 pos = nextAnchor.AnchorWorldPosition;
            float dist = Vector3.Distance(centerTransform.position, pos);

            var locked = new FocusTarget(
                this,
                logical,
                nextAnchor,
                dist,
                pos
            );

            Model.SetLocked(locked);
        }

        protected void ClearLock()
        {
            Model.ClearLocked();
        }

        // ====================================================================
        // Hover picking implementation
        // ====================================================================

        private bool TryPickTargetFromRay(Ray ray, out FocusTarget bestTarget)
        {
            bestTarget = null;
            float bestT = float.PositiveInfinity;

            _anchorsBuffer.Clear();
            Core.Registry.GetAllNonAlloc<TargetableComponent>(_anchorsBuffer);
            var anchors = _anchorsBuffer;

            for (int i = 0; i < anchors.Count; i++)
            {
                var anchor = anchors[i];
                if (anchor == null)
                    continue;

                var logicalRoot = anchor.LogicalRoot;
                Transform rootTransform = logicalRoot.TargetTransform;
                Vector3 scale = rootTransform.lossyScale;

                float scaleMag = Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
                float radius = hoverBaseWorldRadius * Mathf.Max(scaleMag, 0.0001f) + hoverWorldRadiusBuffer;
                radius = Mathf.Max(radius, hoverMinWorldRadius);

                if (!RaySphereHit(ray, anchor.AnchorWorldPosition, radius, out float t))
                    continue;

                if (hoverMaxRayDistance > 0f && t > hoverMaxRayDistance)
                    continue;

                if (t >= bestT)
                    continue;

                bestT = t;

                Vector3 hitPos = ray.origin + ray.direction * t;
                float worldDistFromCenter = Vector3.Distance(centerTransform.position, hitPos);

                var logical = (ITargetable)logicalRoot;

                bestTarget = new FocusTarget(
                    this,
                    logical,
                    anchor,
                    worldDistFromCenter,
                    hitPos
                );
            }

            return bestTarget != null;
        }

        private static bool RaySphereHit(Ray ray, Vector3 center, float radius, out float t)
        {
            Vector3 oc = ray.origin - center;
            float a = Vector3.Dot(ray.direction, ray.direction);
            float b = 2f * Vector3.Dot(oc, ray.direction);
            float c = Vector3.Dot(oc, oc) - radius * radius;

            float discriminant = b * b - 4f * a * c;
            if (discriminant < 0f)
            {
                t = 0f;
                return false;
            }

            float sqrtD = Mathf.Sqrt(discriminant);
            float t0 = (-b - sqrtD) / (2f * a);
            float t1 = (-b + sqrtD) / (2f * a);

            if (t0 > 0f)
            {
                t = t0;
                return true;
            }

            if (t1 > 0f)
            {
                t = t1;
                return true;
            }

            t = 0f;
            return false;
        }

        // ====================================================================
        // Lock candidate search
        // ====================================================================

        private void FindLockCandidates(List<TargetableComponent> results)
        {
            results.Clear();

            _anchorsBuffer.Clear();
            Core.Registry.GetAllNonAlloc<TargetableComponent>(_anchorsBuffer);
            var anchors = _anchorsBuffer;

            var origin = centerTransform.position;
            Vector3 forward = GetFacingDirection(); // implemented by derived class

            float halfFov = lockFovDegrees * 0.5f;
            float radiusSqr = lockRadius * lockRadius;

            for (int i = 0; i < anchors.Count; i++)
            {
                var anchor = anchors[i];
                if (anchor == null) continue;

                Vector3 pos = anchor.AnchorWorldPosition;
                Vector3 to = pos - origin;

                to.z = 0f;
                float distSqr = to.sqrMagnitude;
                if (distSqr > radiusSqr)
                    continue;

                float dist = Mathf.Sqrt(distSqr);
                if (dist < 0.0001f)
                    continue;

                Vector3 dir = to / dist;
                float angle = Vector3.Angle(forward, dir);
                if (angle > halfFov)
                    continue;

                results.Add(anchor);
            }
        }

        private TargetableComponent GetNextLockCandidateLinear(List<TargetableComponent> candidates, FocusTarget currentLocked)
        {
            if (candidates.Count == 0)
                return null;

            var currentAnchor = currentLocked?.Anchor;

            if (currentAnchor == null)
                return candidates[0];

            int index = candidates.IndexOf(currentAnchor);

            if (index < 0)
                return candidates[0];

            int nextIndex = (index + 1) % candidates.Count;
            return candidates[nextIndex];
        }

        /// <summary>
        /// Derived classes define how this targeter is "facing" in the XY plane.
        /// Player: mover.Facing or transform.right
        /// AI: nav agent desired velocity, or look direction, etc.
        /// </summary>
        protected abstract Vector3 GetFacingDirection();

        // ====================================================================
        // Gizmos
        // ====================================================================

        protected virtual void OnDrawGizmosSelected()
        {
            if (centerTransform == null || targetCamera == null)
                return;

            // 1) All anchors
            if (_drawAllAnchors)
            {
                _anchorsBuffer.Clear();
                Core.Registry.GetAllNonAlloc<TargetableComponent>(_anchorsBuffer);

                Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
                foreach (var anchor in _anchorsBuffer)
                {
                    if (anchor == null) continue;
                    Gizmos.DrawSphere(anchor.AnchorWorldPosition, 0.05f);
                }
            }

            // 2) Hover / Locked / Current (using live positions)
            if (_drawHoverGizmos && Model != null)
            {
                // Ray from camera through mouse (for debugging hover)
#if UNITY_EDITOR
                Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
                Gizmos.color = new Color(1f, 1f, 0f, 0.5f);
                Gizmos.DrawRay(ray.origin, ray.direction * 100f);
#endif

                var hover = Model.Hover;
                if (hover != null)
                {
                    Vector3 hoverPos = hover.Anchor != null
                        ? hover.Anchor.AnchorWorldPosition
                        : hover.LogicalTarget != null
                            ? hover.LogicalTarget.TargetPosition
                            : hover.WorldPosition;

                    Gizmos.color = Color.yellow;
                    Gizmos.DrawSphere(hoverPos, 0.1f);
                }

                var locked = Model.Locked;
                if (locked != null)
                {
                    Vector3 lockedPos = locked.Anchor != null
                        ? locked.Anchor.AnchorWorldPosition
                        : locked.LogicalTarget != null
                            ? locked.LogicalTarget.TargetPosition
                            : locked.WorldPosition;

                    Gizmos.color = Color.cyan;
                    Gizmos.DrawSphere(lockedPos, 0.12f);
                }
            }

            // 3) Lock radius + FOV cone and FoV candidates
            if (_drawLockGizmos)
            {
                Vector3 origin = centerTransform.position;
                Vector3 forward = GetFacingDirection();

                Gizmos.color = new Color(0f, 0.5f, 1f, 0.25f);
                Gizmos.DrawWireSphere(origin, lockRadius);

                float halfFov = lockFovDegrees * 0.5f;
                Vector3 axis = Vector3.forward;

                Quaternion leftRot = Quaternion.AngleAxis(-halfFov, axis);
                Quaternion rightRot = Quaternion.AngleAxis(halfFov, axis);

                Vector3 leftDir = leftRot * forward;
                Vector3 rightDir = rightRot * forward;
                leftDir.z = rightDir.z = 0f;

                Gizmos.color = new Color(0f, 0.5f, 1f, 0.5f);
                Gizmos.DrawRay(origin, forward * lockRadius);
                Gizmos.DrawRay(origin, leftDir.normalized * lockRadius);
                Gizmos.DrawRay(origin, rightDir.normalized * lockRadius);

                _lockCandidates.Clear();
                FindLockCandidates(_lockCandidates);

                Gizmos.color = new Color(0f, 0.5f, 1f, 0.75f);
                foreach (var anchor in _lockCandidates)
                {
                    if (anchor == null) continue;
                    Gizmos.DrawWireSphere(anchor.AnchorWorldPosition, 0.12f);
                }
            }
        }
    }
}
