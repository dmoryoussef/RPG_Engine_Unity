using System.Collections.Generic;
using UnityEngine;
using Player; // For PlayerMover2D

namespace Targeting
{
    /// <summary>
    /// Centralized targeting adapter that:
    /// - Maintains a TargetingContextModel for hover, locked, focus, current targets
    /// - Provides mouse-hover picking (ray-sphere) against TargetableComponent anchors
    /// - Supports lock-on via FOV + radius + cycling
    /// - Exposes runtime FocusTargets + debug labels for Inspector/UI
    ///
    /// Designed for top-down 2D where gameplay happens in the XY plane
    /// and PlayerMover2D.Facing is a Vector2 in that same plane.
    /// </summary>
    public sealed class TargeterComponent : MonoBehaviour, ITargeter
    {
        // ====================================================================
        // Core model & references
        // ====================================================================

        [Header("Core References")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Transform playerCenter;

        /// <summary>
        /// Core targeting model consumed by other systems (interaction, combat, etc).
        /// Single writer: this component.
        /// </summary>
        public TargetingContextModel Model { get; private set; }

        private PlayerMover2D _mover2D;

        // ====================================================================
        // Hover picking configuration
        // ====================================================================

        [Header("Hover Picking (Ray-Sphere)")]
        [Tooltip("Base world radius multiplier for hover picking spheres.")]
        [SerializeField] private float hoverBaseWorldRadius = 0.1f;

        [Tooltip("Minimum world radius, even for very small targets.")]
        [SerializeField] private float hoverMinWorldRadius = 0.05f;

        [Tooltip("Extra buffer added to the scaled radius for easier selection.")]
        [SerializeField] private float hoverWorldRadiusBuffer = 0.05f;

        [Tooltip("Optional max distance along the hover ray. <= 0 means infinite.")]
        [SerializeField] private float hoverMaxRayDistance = 0f;

        // ====================================================================
        // Lock-On configuration
        // ====================================================================

        [Header("Lock-On (FOV + Radius)")]
        [Tooltip("Maximum world-space distance for lock-on candidates.")]
        [SerializeField] private float lockRadius = 5f;

        [Tooltip("Field-of-view angle (degrees) for lock-on.")]
        [SerializeField] private float lockFovDegrees = 80f;

        // ====================================================================
        // Input
        // ====================================================================

        [Header("Input")]
        [SerializeField] private KeyCode lockFromHoverKey = KeyCode.Q;
        [SerializeField] private KeyCode cycleLockKey = KeyCode.E;
        [SerializeField] private KeyCode clearLockKey = KeyCode.R;

        [Header("Debug Logging")]
        [SerializeField] private bool logCurrentTargetChanges = false;

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

        [Tooltip("Current target that systems should act on.")]
        public FocusTarget Current;

        [Header("Runtime Debug Labels")]
        [SerializeField] private string debugHoverLabel = "(null)";
        [SerializeField] private string debugLockedLabel = "(null)";
        [SerializeField] private string debugFocusLabel = "(null)";
        [SerializeField] private string debugCurrentLabel = "(null)";

        // ====================================================================
        // Debug Gizmos
        // ====================================================================

        [Header("Debug Gizmos")]
        [SerializeField] private bool _drawHoverGizmos = true;
        [SerializeField] private bool _drawLockGizmos = true;
        [SerializeField] private bool _drawAllAnchors = false;

        // Lock-on candidate cache (reused every frame).
        private readonly List<TargetableComponent> _lockCandidates = new();

        // Registry query buffer for all TargetableComponent anchors.
        private readonly List<TargetableComponent> _anchorsBuffer = new();

        // ====================================================================
        // UNITY LIFECYCLE
        // ====================================================================

        private void Awake()
        {
            if (playerCamera == null)
                playerCamera = Camera.main;

            if (playerCenter == null)
                playerCenter = transform;

            _mover2D = GetComponent<PlayerMover2D>();
            if (_mover2D == null)
            {
                Debug.LogWarning("[Targeting] TargeterComponent could not find PlayerMover2D on this GameObject. " +
                                 "FOV will fall back to transform.right.");
            }

            Model = new TargetingContextModel();

            // TargetingContextModel uses FocusChange for these three:
            //  public event Action<FocusChange> HoverChanged;
            //  public event Action<FocusChange> LockedChanged;
            //  public event Action<FocusChange> FocusChanged;
            // and Action<FocusTarget> for CurrentTargetChanged. :contentReference[oaicite:1]{index=1}
            Model.HoverChanged += OnHoverChanged;
            Model.LockedChanged += OnLockedChanged;
            Model.FocusChanged += OnFocusChanged;
            Model.CurrentTargetChanged += OnCurrentChanged;

            if (logCurrentTargetChanges)
            {
                Model.CurrentTargetChanged += t =>
                    Debug.Log($"[Targeting] Current -> {t?.TargetLabel ?? "null"}");
            }
        }

        private void OnDestroy()
        {
            if (Model == null) return;

            Model.HoverChanged -= OnHoverChanged;
            Model.LockedChanged -= OnLockedChanged;
            Model.FocusChanged -= OnFocusChanged;
            Model.CurrentTargetChanged -= OnCurrentChanged;
        }

        private void Update()
        {
            if (Model == null)
                return;

            UpdateHoverFromMouse();
            HandleLockInput();

            // No Model.UpdateFrame() here – TargetingContextModel recomputes
            // CurrentTarget internally whenever we call Set/Clear. :contentReference[oaicite:2]{index=2}
        }

        // ====================================================================
        // Event handlers – sync inspector fields & labels
        // ====================================================================

        private void OnHoverChanged(FocusChange change)
        {
            var prev = change.Previous;
            var current = change.Current;

            var prevAnchor = prev?.Anchor;
            var newAnchor = current?.Anchor;

            // previous hover lost
            if (prevAnchor != null && prevAnchor != newAnchor)
                prevAnchor.RaiseUnhovered(prev);

            // new hover gained
            if (newAnchor != null && newAnchor != prevAnchor)
                newAnchor.RaiseHovered(current);

            CurrentHover = current;
            debugHoverLabel = current?.TargetLabel ?? "(null)";
        }


        private void OnLockedChanged(FocusChange change)
        {
            var prev = change.Previous;
            var current = change.Current;

            var prevAnchor = prev?.Anchor;
            var newAnchor = current?.Anchor;

            // old locked target is no longer locked
            if (prevAnchor != null && prevAnchor != newAnchor)
                prevAnchor.RaiseUntargeted(prev);

            // new locked target
            if (newAnchor != null && newAnchor != prevAnchor)
                newAnchor.RaiseTargeted(current);

            CurrentLocked = current;
            debugLockedLabel = current?.TargetLabel ?? "(null)";
        }


        private void OnFocusChanged(FocusChange change)
        {
            // No hooks here by default, so we don't double-fire anything.
            // Focus is a *derived* notion based on hover/locked, so systems
            // that care can just subscribe to Model.FocusChanged directly.

            CurrentFocus = change.Current;
            debugFocusLabel = change.Current?.TargetLabel ?? "(null)";
        }

        private void OnCurrentChanged(FocusTarget target)
        {
            // Again, no hooks here by default: hover/lock hooks have already run.
            // Current is just "the thing other systems should use right now".

            Current = target;
            debugCurrentLabel = target?.TargetLabel ?? "(null)";
        }


        // ====================================================================
        // Hover: ray-based picking against all TargetableComponent anchors
        // ====================================================================

        private void UpdateHoverFromMouse()
        {
            if (playerCamera == null)
                return;

            Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);

            if (TryPickTargetFromRay(ray, out var bestTarget))
                Model.SetHover(bestTarget);
            else
                Model.ClearHover();
        }

        /// <summary>
        /// Centralized ray-based picking logic for hover.
        /// Uses a ray-sphere test around each anchor position.
        /// Sphere radius is derived from logical root scale + configurable base radius.
        /// </summary>
        private bool TryPickTargetFromRay(Ray ray, out FocusTarget bestTarget)
        {
            bestTarget = null;
            float bestT = float.PositiveInfinity;

            // Pull all anchors from the WorldRegistry (GC-free with NonAlloc).
            _anchorsBuffer.Clear();
            World.Registry.GetAllNonAlloc<TargetableComponent>(_anchorsBuffer);
            var anchors = _anchorsBuffer;

            for (int i = 0; i < anchors.Count; i++)
            {
                var anchor = anchors[i];
                if (anchor == null)
                    continue;

                // Use logical root's scale as a size hint
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
                float worldDistFromPlayer = Vector3.Distance(playerCenter.position, hitPos);

                var logical = (ITargetable)logicalRoot;

                bestTarget = new FocusTarget(
                    this,
                    logical,
                    anchor,
                    worldDistFromPlayer,
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

            // choose nearest positive
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
        // Lock-On: FOV + radius + cycling
        // ====================================================================

        private void HandleLockInput()
        {
            if (Input.GetKeyDown(lockFromHoverKey))
                LockFromHover();

            if (Input.GetKeyDown(cycleLockKey))
                CycleLock();

            if (Input.GetKeyDown(clearLockKey))
                Model.ClearLocked();
        }

        private void LockFromHover()
        {
            var hover = Model.Hover;
            if (hover == null)
                return;

            var logical = hover.LogicalTarget;
            var anchor = hover.Anchor;

            if (logical == null || anchor == null)
                return;

            Vector3 worldPos = anchor.AnchorWorldPosition;
            float dist = Vector3.Distance(playerCenter.position, worldPos);

            var locked = new FocusTarget(
                this,
                logical,
                anchor,
                dist,
                worldPos
            );

            Model.SetLocked(locked);
        }

        private void CycleLock()
        {
            _lockCandidates.Clear();
            FindLockCandidates(_lockCandidates);

            var currentLocked = Model.Locked;
            var next = GetNextLockCandidate(_lockCandidates, currentLocked);

            if (next == null)
            {
                Model.ClearLocked();
                return;
            }

            var logical = (ITargetable)next.LogicalRoot;

            Vector3 pos = next.AnchorWorldPosition;
            float dist = Vector3.Distance(playerCenter.position, pos);

            var locked = new FocusTarget(
                this,
                logical,
                next,
                dist,
                pos
            );

            Model.SetLocked(locked);
        }

        private void FindLockCandidates(List<TargetableComponent> results)
        {
            results.Clear();

            // Pull anchors from the registry instead of a static list.
            _anchorsBuffer.Clear();
            World.Registry.GetAllNonAlloc<TargetableComponent>(_anchorsBuffer);
            var anchors = _anchorsBuffer;

            var origin = playerCenter.position;
            Vector3 forward = GetFacingDirection(); // XY-plane facing

            float halfFov = lockFovDegrees * 0.5f;
            float radiusSqr = lockRadius * lockRadius;

            for (int i = 0; i < anchors.Count; i++)
            {
                var anchor = anchors[i];
                if (anchor == null) continue;

                Vector3 pos = anchor.AnchorWorldPosition;
                Vector3 to = pos - origin;

                // Work in XY plane; ignore Z for FOV and distance gating
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

        private TargetableComponent GetNextLockCandidate(List<TargetableComponent> candidates, FocusTarget currentLocked)
        {
            if (candidates.Count == 0)
                return null;

            if (currentLocked?.Anchor == null)
                return GetBestAligned(candidates);

            var currentAnchor = currentLocked.Anchor;
            int index = candidates.IndexOf(currentAnchor);

            if (index < 0)
                return GetBestAligned(candidates);

            int nextIndex = (index + 1) % candidates.Count;
            return candidates[nextIndex];
        }

        private TargetableComponent GetBestAligned(List<TargetableComponent> candidates)
        {
            if (candidates.Count == 0)
                return null;

            Vector3 origin = playerCenter.position;
            Vector3 forward = GetFacingDirection();

            float bestDot = float.NegativeInfinity;
            TargetableComponent best = null;

            for (int i = 0; i < candidates.Count; i++)
            {
                var anchor = candidates[i];
                if (anchor == null) continue;

                Vector3 dir = anchor.AnchorWorldPosition - origin;
                dir.z = 0f;
                if (dir.sqrMagnitude < 0.0001f)
                    continue;

                dir.Normalize();
                float dot = Vector3.Dot(forward, dir);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = anchor;
                }
            }

            return best;
        }

        private Vector3 GetFacingDirection()
        {
            if (_mover2D != null)
            {
                Vector2 f2 = _mover2D.Facing;
                if (f2.sqrMagnitude > 0.0001f)
                    return new Vector3(f2.x, f2.y, 0f).normalized;
            }

            Vector3 forward = playerCenter != null ? playerCenter.right : Vector3.right;
            forward.z = 0f;

            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.right;

            return forward.normalized;
        }

        // ====================================================================
        // Debug Gizmos
        // ====================================================================

        private void OnDrawGizmosSelected()
        {
            if (playerCenter == null || playerCamera == null)
                return;

            // ---------------------------------------------------------
            // 1) All anchors (optional)
            // ---------------------------------------------------------
            if (_drawAllAnchors)
            {
                _anchorsBuffer.Clear();
                World.Registry.GetAllNonAlloc<TargetableComponent>(_anchorsBuffer);

                Gizmos.color = new Color(1f, 1f, 1f, 0.25f); // faint white
                foreach (var anchor in _anchorsBuffer)
                {
                    if (anchor == null) continue;
                    Gizmos.DrawSphere(anchor.AnchorWorldPosition, 0.05f);
                }
            }

            // ---------------------------------------------------------
            // 2) Hover / Locked / Current targets
            // ---------------------------------------------------------
            if (_drawHoverGizmos && Model != null)
            {
                // Draw ray from camera through mouse (approx hover ray)
                Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
                Gizmos.color = new Color(1f, 1f, 0f, 0.5f); // yellow
                Gizmos.DrawRay(ray.origin, ray.direction * 100f);

                // Hover target
                if (Model.Hover != null)
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawSphere(Model.Hover.Anchor.AnchorWorldPosition, 0.1f);
                }

                // Locked target
                if (Model.Locked != null)
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawSphere(Model.Locked.Anchor.AnchorWorldPosition, 0.12f);
                }

                // Current (resolved) target
                if (Model.CurrentTarget != null)
                {
                    Gizmos.color = Color.green;
                    Vector3 position = Model.CurrentTarget.Anchor.AnchorWorldPosition;
                    Gizmos.DrawSphere(position, 0.14f);

                    Gizmos.DrawLine(playerCenter.position, position);
                }
            }

            // ---------------------------------------------------------
            // 3) Lock radius + FOV cone around the player (XY plane)
            // ---------------------------------------------------------
            if (_drawLockGizmos)
            {
                // Lock radius
                Gizmos.color = new Color(0f, 0.5f, 1f, 0.25f); // bluish transparent
                Gizmos.DrawWireSphere(playerCenter.position, lockRadius);

                // FOV cone in XY plane, rotating around Z axis
                float halfFov = lockFovDegrees * 0.5f;
                Vector3 origin = playerCenter.position;

                Vector3 forward = GetFacingDirection();    // lies in XY
                Vector3 axis = Vector3.forward;            // rotate around Z to sweep in XY

                Quaternion leftRot = Quaternion.AngleAxis(-halfFov, axis);
                Quaternion rightRot = Quaternion.AngleAxis(halfFov, axis);

                Vector3 leftDir = leftRot * forward;
                Vector3 rightDir = rightRot * forward;

                Gizmos.color = new Color(0f, 0.5f, 1f, 0.5f);

                Gizmos.DrawRay(origin, forward * lockRadius);
                Gizmos.DrawRay(origin, leftDir * lockRadius);
                Gizmos.DrawRay(origin, rightDir * lockRadius);

                // ---------------------------------------------
                // Circles around all FoV lock candidates
                // ---------------------------------------------
                _lockCandidates.Clear();
                FindLockCandidates(_lockCandidates);

                Gizmos.color = new Color(0f, 0.5f, 1f, 0.75f); // slightly stronger blue
                foreach (var anchor in _lockCandidates)
                {
                    if (anchor == null)
                        continue;

                    // Draw a small circle around each FoV candidate
                    Gizmos.DrawWireSphere(anchor.AnchorWorldPosition, 0.12f);
                }
            }
        }



    }
}
