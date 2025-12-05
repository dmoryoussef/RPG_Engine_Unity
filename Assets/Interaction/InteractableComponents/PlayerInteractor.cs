using Player;
using UnityEngine;
using Logging;

namespace Interaction
{
    /// <summary>
    /// Player interactor with point-based mouse hover and gizmo:
    ///
    /// Hover:
    /// - Convert mouse position to a world point on z = 0.
    /// - Choose the InteractableBase whose world bounds contain that point.
    /// - Prefer the closest one to the player (and highest SelectionPriority for ties).
    /// - UpdateCurrentTarget() (from InteractorBase) sets the hovered target.
    ///
    /// Locked:
    /// - Right-click on a hovered interactable: locks it as lockedTarget.
    /// - Right-click empty space: clears lock.
    ///
    /// Interacting:
    /// - Uses distance + facing gates:
    ///   - Distance <= interactMaxDistance
    ///   - Facing: dot(playerFacing, toTarget) >= interactFacingDotThreshold
    ///
    /// Gizmos:
    /// - Shows a line from player to mouse, and colors it green/red based on gating.
    /// </summary>
    public class PlayerInteractor : InteractorBase
    {
        [Header("Dependencies")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private PlayerMover2D mover;

        [Header("Interaction Gating")]
        [SerializeField, Tooltip("Maximum distance from player to target for interaction.")]
        private float interactMaxDistance = 1.5f;

        [SerializeField, Range(-1f, 1f), Tooltip("Minimum dot product between player's facing and direction to target.")]
        private float interactFacingDotThreshold = 0.4f;

        [SerializeField, Tooltip("Fallback facing direction if mover is missing or idle.")]
        private Vector2 idleFacing2D = Vector2.down;

        [Header("Runtime (Read-Only)")]
        [SerializeField, Tooltip("Distance from the player to the active target (locked or hovered).")]
        private float hoverDistanceFromPlayer = 0f;

        [SerializeField, Tooltip("Is the player currently facing the active target sufficiently?")]
        private bool facingTarget = false;

        [SerializeField, Tooltip("Is the active target within interactMaxDistance?")]
        private bool inRange = false;

        [SerializeField, Tooltip("Would an interaction attempt succeed right now (distance + facing)?")]
        private bool canInteract = false;

        [SerializeField]
        private bool lastInteractSucceeded;

        [SerializeField]
        private float lastInteractTime = -999f;

        [Header("Debug Targets")]
        [SerializeField, Tooltip("Current hovered interactable (under the mouse).")]
        private InteractableBase hoverTarget;

        [SerializeField, Tooltip("Last explicitly locked interactable (via right-click).")]
        private InteractableBase lockedTarget;

        [Header("Debug Logging")]
        [SerializeField, Tooltip("If true, PlayerInteractor will emit detailed interaction logs via GameLog.")]
        private bool debugLogging = false;

        private const string SystemTag = "PlayerInteractor";

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
            // 1) Mouse-driven hover picking (InteractorBase sets currentTarget via TryPick()).
            UpdateCurrentTarget();           // uses our overridden TryPick()
            hoverTarget = currentTarget;     // cache for inspector clarity

            // 2) Right-click lock/unlock behavior.
            if (Input.GetMouseButtonDown(1))
            {
                if (hoverTarget != null)
                {
                    // Right-click on something → lock it.
                    lockedTarget = hoverTarget;
                }
                else
                {
                    // Right-click empty space → clear lock.
                    lockedTarget = null;
                }
            }

            // 3) Choose an "active" target for debug/gating: locked if present, else hover.
            var activeTarget = lockedTarget ? lockedTarget : hoverTarget;
            RefreshGatingDebug(activeTarget);

            // 4) Handle key-driven interactions:
            //    - First check interactables on the hovered object;
            //    - If none match, check interactables on the locked object.
            bool interacted =
                TryHandleKeyInteractionsForRoot(hoverTarget) ||
                TryHandleKeyInteractionsForRoot(lockedTarget);

            if (interacted)
            {
                lastInteractTime = Time.realtimeSinceStartup;
            }
        }

        /// <summary>
        /// Try to dispatch key presses to interactables attached to the given root.
        /// Returns true if any interaction was triggered.
        /// </summary>
        private bool TryHandleKeyInteractionsForRoot(InteractableBase root)
        {
            if (!root)
                return false;

            // Get all interactables on the same GameObject.
            var interactables = root.GetComponents<InteractableBase>();
            if (interactables.Length == 0)
                interactables = new[] { root }; // safety fallback

            foreach (var it in interactables)
            {
                if (!it) continue;

                var key = it.InteractionKey;
                if (key == KeyCode.None)
                    continue; // this interactable doesn't respond to keys

                if (Input.GetKeyDown(key))
                {
                    bool ok = TryInteractWith(it);
                    lastInteractSucceeded = ok;
                    return ok;
                }
            }

            return false;
        }

        /// <summary>
        /// Try to interact with a specific interactable, applying distance + facing gates.
        /// </summary>
        private bool TryInteractWith(InteractableBase target)
        {
            if (!target)
                return false;

            bool inRangeLocal = IsInRange(target, out float dist);
            bool facingOkLocal = IsFacingTarget(target, out float dot);

            if (!inRangeLocal || !facingOkLocal)
            {
                if (debugLogging)
                {
                    string resultStr;
                    if (!inRangeLocal && !facingOkLocal)
                        resultStr = "BlockedDistanceAndFacing";
                    else if (!inRangeLocal)
                        resultStr = "BlockedDistance";
                    else
                        resultStr = "BlockedFacing";

                    string targetId = target.InteractableId ?? target.name;
                    string msg =
                        $"target={targetId}, dist={dist:F2}, dot={dot:F2}, " +
                        $"threshold={interactFacingDotThreshold:F2}, maxDist={interactMaxDistance:F2}";

                    GameLog.Log(
                        this,
                        system: SystemTag,
                        action: "TryInteractWith",
                        result: resultStr,
                        message: msg);
                }



                return false;
            }

            bool ok = target.OnInteract();

            if (debugLogging)
            {
                string targetId = target.InteractableId ?? target.name;
                string msg = $"target={targetId}, dist={dist:F2}, dot={dot:F2}";

                GameLog.Log(
                    this,
                    system: SystemTag,
                    action: "TryInteractWith",
                    result: ok ? "Success" : "Failed",
                    message: msg);
            }

            return ok;
        }

        // =====================================================================
        //  PICKING: point-based mouse hover (no rayDistance)
        // =====================================================================

        protected override Vector3 GetOrigin()
        {
            Vector3 p = transform.position;
            p.z = 0f;
            return p;
        }

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

        public override bool TryPick(out InteractableBase target, out float distance)
        {
            target = null;
            distance = float.MaxValue;

            if (!TryGetMouseWorldOnPlane(out var mouseWorld))
                return false;

            Vector3 playerPos = GetOrigin();

            UpdateLastRayDebug(playerPos, mouseWorld);

            target = FindBestCandidateAtPoint(mouseWorld, playerPos, out distance);
            return target != null;
        }

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

        private void UpdateLastRayDebug(Vector3 origin, Vector3 point)
        {
            lastOrigin = origin;

            Vector3 dir = point - origin;
            if (dir.sqrMagnitude < 1e-6f)
                dir = Vector3.right;

            lastDir = dir.normalized;
        }

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

                if (!b.Contains(point))
                    continue;

                // Distance from player to interactable center.
                float playerDist = Vector3.Distance(playerPos, b.center);
                float priority = it.SelectionPriority;

                if (priority > bestPriority ||
                    (Mathf.Approximately(priority, bestPriority) && playerDist < bestDistance))
                {
                    best = it;
                    bestDistance = playerDist;
                    bestPriority = priority;
                }
            }

            return best;
        }

        // =====================================================================
        //  INTERACTING: distance + facing gates
        // =====================================================================

        public override bool TryInteract()
        {
            // Use lockedTarget if present, else fall back to hoverTarget.
            var target = lockedTarget ? lockedTarget : hoverTarget;

            if (!target)
            {
                // Fallback: if neither is set, try a one-off pick.
                if (!TryPick(out target, out _))
                {
                    lastPicked = "<none>";

                    if (debugLogging)
                    {
                        GameLog.Log(
                            this,
                            system: SystemTag,
                            action: "TryInteract",
                            result: "NoTarget",
                            message: "No interactable under cursor or locked.");
                    }

                    lastInteractSucceeded = false;
                    return false;
                }
            }

            lastPicked = target.InteractableId ?? target.name;

            bool inRangeLocal = IsInRange(target, out float dist);
            bool facingOkLocal = IsFacingTarget(target, out float dot);

            hoverDistanceFromPlayer = dist;
            inRange = inRangeLocal;
            facingTarget = facingOkLocal;
            canInteract = inRange && facingTarget;

            if (!canInteract)
            {
                if (debugLogging)
                {
                    string resultStr;
                    if (!inRangeLocal && !facingOkLocal)
                        resultStr = "BlockedDistanceAndFacing";
                    else if (!inRangeLocal)
                        resultStr = "BlockedDistance";
                    else
                        resultStr = "BlockedFacing";

                    string msg =
                        $"target={lastPicked}, dist={dist:F2}, dot={dot:F2}, " +
                        $"threshold={interactFacingDotThreshold:F2}, maxDist={interactMaxDistance:F2}";

                    GameLog.Log(
                        this,
                        system: SystemTag,
                        action: "TryInteract",
                        result: resultStr,
                        message: msg);
                }

                lastInteractSucceeded = false;
                return false;
            }

            bool ok = target.OnInteract();
            lastInteractSucceeded = ok;
            if (ok)
                lastInteractTime = Time.realtimeSinceStartup;

            if (debugLogging)
            {
                string msg = $"target={lastPicked}, dist={dist:F2}, dot={dot:F2}";
                GameLog.Log(
                    this,
                    system: SystemTag,
                    action: "TryInteract",
                    result: ok ? "Success" : "Failed",
                    message: msg);
            }

            return ok;
        }

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
                dot = 1f;
                return true;
            }

            Vector3 facing3D = new Vector3(facing2D.x, facing2D.y, 0f).normalized;
            Vector3 toTargetNorm = toTarget.normalized;
            dot = Vector3.Dot(facing3D, toTargetNorm);

            return dot >= interactFacingDotThreshold;
        }

        private void RefreshGatingDebug(InteractableBase activeTarget)
        {
            if (!activeTarget)
            {
                hoverDistanceFromPlayer = 0f;
                inRange = false;
                facingTarget = false;
                canInteract = false;
                return;
            }

            bool inRangeLocal = IsInRange(activeTarget, out float dist);
            bool facingOkLocal = IsFacingTarget(activeTarget, out _);
            bool canInteractLocal = inRangeLocal && facingOkLocal;

            hoverDistanceFromPlayer = dist;
            inRange = inRangeLocal;
            facingTarget = facingOkLocal;
            canInteract = canInteractLocal;
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
                lastFailReason: null
            );
        }


        private void OnDrawGizmosSelected()
        {
            if (!TryGetMouseWorldOnPlane(out var mouseWorld))
                return;

            Vector3 playerPos = GetOrigin();

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(playerPos, mouseWorld);
            Gizmos.DrawSphere(mouseWorld, 0.05f);

            var gizmoTarget = hoverTarget ? hoverTarget : lockedTarget;
            bool gizmoCanInteract = false;

            if (gizmoTarget != null)
            {
                if (Application.isPlaying)
                {
                    gizmoCanInteract = canInteract;
                }
                else
                {
                    bool inRangeLocal = IsInRange(gizmoTarget, out _);
                    bool facingOkLocal = IsFacingTarget(gizmoTarget, out _);
                    gizmoCanInteract = inRangeLocal && facingOkLocal;
                }
            }

            Gizmos.color = gizmoCanInteract ? Color.green : Color.red;
            Gizmos.DrawLine(playerPos, mouseWorld);
        }
    }
}
