using Player;  
using UI;
using UnityEngine;

namespace Interaction
{
    /// <summary>
    /// Player interactor with point-based mouse hover and color-coded gizmo:
    ///
    /// Hover:
    /// - Convert mouse position to a world point on z = 0.
    /// - Choose the InteractableBase whose world bounds contain that point.
    /// - Prefer the closest one to the player (and highest SelectionPriority for ties).
    /// - UpdateCurrentTarget() (from InteractorBase) then fires OnEnterRange / OnLeaveRange.
    ///
    /// Interact:
    /// - When the interact key is pressed:
    ///     * Player must be within interactMaxDistance of the target.
    ///     * Player must be facing the target sufficiently (optional).
    ///     * Then target.OnInteract() is called.
    ///
    /// Gizmo:
    /// - Draws a line from player toward the mouse.
    /// - Line is GREEN if:
    ///     * there is a currentTarget, and
    ///     * distance <= interactMaxDistance, and
    ///     * facing dot >= interactFacingDotThreshold.
    /// - Otherwise the line is RED.
    ///
    /// No colliders or rigidbodies are required. InteractableBase uses world-space Bounds.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerInteractor : InteractorBase
    {
        [Header("References")]
        [SerializeField]
        private Camera playerCamera;

        [SerializeField, Tooltip("Provides player Facing direction (Vector2) in world space.")]
        private PlayerMover2D mover;

        [Header("Input")]
        [SerializeField]
        private KeyCode interactKey = KeyCode.E;

        [Header("Interaction Gating")]
        [SerializeField, Tooltip("Maximum distance from player to target to allow interaction.")]
        private float interactMaxDistance = 1.5f;

        [Range(-1f, 1f)]
        [SerializeField, Tooltip("Minimum player-facing dot to allow interaction (1 = exact, 0 = 90°, -1 = opposite).")]
        private float interactFacingDotThreshold = 0.4f;

        [SerializeField, Tooltip("Fallback facing direction if mover is missing or idle.")]
        private Vector2 idleFacing2D = Vector2.down;

        [Header("Runtime (Read-Only)")]
        [SerializeField, Tooltip("Distance from the player to the current hovered target.")]
        private float hoverDistanceFromPlayer = 0f;

        [SerializeField, Tooltip("Is the player currently facing the hovered target sufficiently?")]
        private bool facingTarget = false;

        [SerializeField, Tooltip("Is the hovered target within interactMaxDistance?")]
        private bool inRange = false;

        [SerializeField, Tooltip("Would an interaction attempt succeed right now (distance + facing)?")]
        private bool canInteract = false;

        [SerializeField]
        private bool lastInteractSucceeded;

        [SerializeField]
        private float lastInteractTime = -999f;

        private void Awake()
        {
            if (!playerCamera)
                playerCamera = Camera.main;

            // These base-level fields aren't used for hover in the new system,
            // but we set them to benign values so they don't mislead.
            probeMode = ProbeMode.Ray;
            minFacingDot = -1f;   // no angular gating at the base level
            rayDistance = 0f;     // not used by our TryPick override
        }

        private void Reset()
        {
            if (!playerCamera)
                playerCamera = Camera.main;

            if (!mover)
                mover = GetComponent<PlayerMover2D>();

            probeMode = ProbeMode.Ray;
            minFacingDot = -1f;
            rayDistance = 0f;
        }

        private void Update()
        {
            // 1) Mouse-driven hover picking (sets currentTarget via InteractorBase).
            UpdateCurrentTarget();   // uses our overridden TryPick()

            // 2) Keep debug bools in sync with the current target.
            RefreshGatingDebug(currentTarget);

            // 3) Interact on key press, gated by distance + facing.
            if (Input.GetKeyDown(interactKey))
            {
                bool ok = TryInteract();
                lastInteractSucceeded = ok;
                lastInteractTime = Time.realtimeSinceStartup;
            }
        }

        /// <summary>
        /// Build an interaction gating snapshot for the given target.
        /// This does NOT modify gameplay state; it only queries distance and facing
        /// using the same logic as TryInteract / RefreshGatingDebug.
        /// </summary>
        public InteractionGateInfo BuildGateInfo(InteractableBase target)
        {
            if (!target)
                return InteractionGateInfo.Empty;

            bool inRangeLocal = IsInRange(target, out float dist);
            bool facingOkLocal = IsFacingTarget(target, out float dot);
            bool canInteractLocal = inRangeLocal && facingOkLocal;

            return new InteractionGateInfo(
                interactorRoot: gameObject,
                interactableRoot: target.gameObject,
                inRange: inRangeLocal,
                distance: dist,
                maxDistance: interactMaxDistance,
                facingOk: facingOkLocal,
                facingDot: dot,
                facingThreshold: interactFacingDotThreshold,
                canInteract: canInteractLocal,
                lastFailReason: null // can be filled later by interactable if desired
            );
        }

        // =====================================================================
        //  PICKING: point-based mouse hover (no rayDistance)
        // =====================================================================

        /// <summary>
        /// Player position on z=0 plane, used for distance + gizmo.
        /// </summary>
        protected override Vector3 GetOrigin()
        {
            Vector3 p = transform.position;
            p.z = 0f;
            return p;
        }

        /// <summary>
        /// For gizmos only: direction from player toward mouse on the gameplay plane.
        /// Hover picking itself is done by Bounds.Contains(mouseWorld).
        /// </summary>
        protected override Vector3 GetFacingDir()
        {
            if (!TryGetMouseWorldOnPlane(out var mouseWorld))
                return Vector3.right;

            Vector3 origin = GetOrigin();
            Vector3 dir = mouseWorld - origin;

            if (dir.sqrMagnitude < 1e-6f)
                dir = Vector3.right;

            return dir.normalized;
        }

        /// <summary>
        /// OVERRIDE: point-based picking under the mouse, independent of rayDistance.
        /// - Converts mouse position to a world point on z=0.
        /// - Chooses the InteractableBase whose bounds contain that point.
        /// - Prefers closest to player (and SelectionPriority for ties).
        /// </summary>
        public override bool TryPick(out InteractableBase target, out float distance)
        {
            target = null;
            distance = float.MaxValue;

            // Get mouse world point on the gameplay plane.
            if (!TryGetMouseWorldOnPlane(out var mouseWorld))
                return false;

            Vector3 playerPos = GetOrigin(); // player position flattened to z=0

            // For gizmo/debug line: store lastOrigin/lastDir so InteractorBase can draw.
            UpdateLastRayDebug(playerPos, mouseWorld);

            // Choose the best interactable at the mouse point.
            target = FindBestCandidateAtPoint(mouseWorld, playerPos, out distance);
            return target != null;
        }

        /// <summary>
        /// Convert mouse screen position to world position on the z = 0 plane.
        /// Returns false if no camera or ray-plane intersection fails.
        /// </summary>
        private bool TryGetMouseWorldOnPlane(out Vector3 mouseWorld)
        {
            mouseWorld = Vector3.zero;

            if (!playerCamera)
            {
                playerCamera = Camera.main;
                if (!playerCamera)
                    return false;
            }

            Ray mouseRay = playerCamera.ScreenPointToRay(Input.mousePosition);
            Plane plane = new Plane(Vector3.forward, Vector3.zero);

            if (!plane.Raycast(mouseRay, out float enter))
                return false;

            mouseWorld = mouseRay.GetPoint(enter);
            return true;
        }

        /// <summary>
        /// Update the lastOrigin / lastDir fields used by InteractorBase for gizmos.
        /// </summary>
        private void UpdateLastRayDebug(Vector3 origin, Vector3 point)
        {
            lastOrigin = origin;

            Vector3 dir = point - origin;
            if (dir.sqrMagnitude < 1e-6f)
                dir = Vector3.right;

            lastDir = dir.normalized;
        }

        /// <summary>
        /// Scan registry candidates and return the best interactable whose bounds
        /// contain the given world-space point. "Best" = closest to player, then by SelectionPriority.
        /// </summary>
        private InteractableBase FindBestCandidateAtPoint(Vector3 point, Vector3 playerPos, out float bestDistance)
        {
            _pool.Clear();
            foreach (var it in GetCandidates())
            {
                if (!it || !it.isActiveAndEnabled)
                    continue;

                _pool.Add(it);
            }

            InteractableBase best = null;
            bestDistance = float.MaxValue;
            float bestPriority = float.NegativeInfinity;

            foreach (var it in _pool)
            {
                Bounds b = it.GetWorldBounds();

                // Check if mouse is inside this interactable's bounds.
                if (!b.Contains(point))
                    continue;

                // Distance from player to this interactable (for gating later).
                float playerDist = Vector3.Distance(playerPos, it.transform.position);

                bool better =
                    playerDist < bestDistance ||
                    (Mathf.Approximately(playerDist, bestDistance) && it.SelectionPriority > bestPriority);

                if (!better)
                    continue;

                best = it;
                bestDistance = playerDist;
                bestPriority = it.SelectionPriority;
            }

            return best;
        }

        // =====================================================================
        //  INTERACTING: distance + facing gates
        // =====================================================================

        /// <summary>
        /// Uses:
        /// - currentTarget (set by hover)
        /// - player → target distance (interactMaxDistance)
        /// - mover.Facing vs direction to target (interactFacingDotThreshold)
        /// Only then calls target.OnInteract().
        /// </summary>
        public override bool TryInteract()
        {
            var target = currentTarget;

            if (!target)
            {
                // Fallback: if hover target wasn't set, we can still try a one-off pick.
                if (!TryPick(out target, out _))
                {
                    lastPicked = "<none>";
                    return false;
                }
            }

            lastPicked = target.InteractableId ?? target.name;

            // Evaluate gates (and keep debug fields in sync).
            bool inRangeLocal = IsInRange(target, out float dist);
            bool facingOkLocal = IsFacingTarget(target, out _);

            hoverDistanceFromPlayer = dist;
            inRange = inRangeLocal;
            facingTarget = facingOkLocal;
            canInteract = inRange && facingTarget;

            if (!canInteract)
                return false;

            // All gates passed: actually interact.
            return target.OnInteract();
        }

        /// <summary>
        /// Distance gate: returns true if target is within interactMaxDistance.
        /// </summary>
        private bool IsInRange(InteractableBase target, out float distance)
        {
            distance = 0f;

            if (!target)
                return false;

            Vector3 playerPos = transform.position;
            Vector3 toTarget = target.transform.position - playerPos;
            distance = toTarget.magnitude;

            return distance <= interactMaxDistance;
        }

        /// <summary>
        /// Facing gate: returns true if the player's facing direction is
        /// sufficiently aligned with the direction to the target.
        /// </summary>
        private bool IsFacingTarget(InteractableBase target, out float dot)
        {
            dot = 0f;

            if (!target)
                return false;

            Vector2 facing2D = mover ? mover.Facing : idleFacing2D;
            if (facing2D.sqrMagnitude < 1e-6f)
                facing2D = idleFacing2D;

            Vector3 playerPos = transform.position;
            Vector3 toTarget = target.transform.position - playerPos;

            if (toTarget.sqrMagnitude < 1e-6f)
            {
                // Same position – treat as always facing.
                dot = 1f;
                return true;
            }

            Vector3 facing3D = new Vector3(facing2D.x, facing2D.y, 0f).normalized;
            Vector3 flatToTarget = new Vector3(toTarget.x, toTarget.y, 0f).normalized;

            dot = Vector3.Dot(facing3D, flatToTarget);
            return dot >= interactFacingDotThreshold;
        }

        /// <summary>
        /// Centralized helper to keep hoverDistanceFromPlayer / inRange /
        /// facingTarget / canInteract in sync with the current target.
        /// Called from Update(), but also used implicitly via TryInteract().
        /// </summary>
        private void RefreshGatingDebug(InteractableBase target)
        {
            if (!target)
            {
                hoverDistanceFromPlayer = 0f;
                inRange = false;
                facingTarget = false;
                canInteract = false;
                return;
            }

            bool inRangeLocal = IsInRange(target, out float dist);
            bool facingOkLocal = IsFacingTarget(target, out _);

            hoverDistanceFromPlayer = dist;
            inRange = inRangeLocal;
            facingTarget = facingOkLocal;
            canInteract = inRange && facingTarget;
        }

        // =====================================================================
        //  GIZMOS: red/green line for range + facing correctness
        // =====================================================================

        /// <summary>
        /// Draws a line from the player toward the mouse:
        /// - GREEN if we have a currentTarget and both:
        ///     * distance <= interactMaxDistance
        ///     * facing dot >= interactFacingDotThreshold
        /// - RED otherwise.
        /// </summary>
        protected override void OnDrawGizmosSelected()
        {
            if (!drawGizmos)
                return;

            Vector3 playerPos = transform.position;
            playerPos.z = 0f;

            // Compute mouse world position on z = 0 plane.
            Vector3 mouseWorld = playerPos + Vector3.right * 2f; // safe fallback
            if (TryGetMouseWorldOnPlane(out var hit))
            {
                mouseWorld = hit;
            }

            bool gizmoCanInteract = false;

            if (currentTarget != null)
            {
                if (Application.isPlaying)
                {
                    // In play mode, just mirror the debug fields from Update().
                    gizmoCanInteract = canInteract;
                }
                else
                {
                    // In edit mode, recompute quickly so the scene view is meaningful.
                    bool inRangeLocal = IsInRange(currentTarget, out _);
                    bool facingOkLocal = IsFacingTarget(currentTarget, out _);
                    gizmoCanInteract = inRangeLocal && facingOkLocal;
                }
            }

            Gizmos.color = gizmoCanInteract ? Color.green : Color.red;
            Gizmos.DrawLine(playerPos, mouseWorld);
        }
    }
}
