using Player;
using UnityEngine;
using Logging;

namespace Interaction
{
    /// <summary>
    /// Player-specific interactor built on top of InteractorBase.
    ///
    /// Responsibilities:
    /// - Defines the player origin and facing direction for gating.
    /// - Consumes the cached currentTarget that InteractorBase maintains
    ///   (typically fed by a TargeterComponent on the same GameObject).
    /// - Handles player input and invokes TryInteract() on demand.
    ///
    /// It no longer performs any picking itself; selection is handled by
    /// Targeter/TargetingContextModel, and InteractorBase caches that as
    /// currentTarget via its Targeter subscriptions.
    /// </summary>
    public sealed class PlayerInteractor : InteractorBase
    {
        [Header("Dependencies")]
        [SerializeField] private PlayerMover2D mover;

        [Header("Facing")]
        [SerializeField, Tooltip("Fallback facing when the mover has no input (idle).")]
        private Vector2 idleFacing2D = Vector2.down;

        [Header("Input")]
        [SerializeField, Tooltip("Primary interaction key used when interacting with the current target.")]
        private KeyCode primaryInteractKey = KeyCode.E;

        [Header("Debug State")]
        [SerializeField, Tooltip("If true, interaction attempts will be logged via GameLog.")]
        private bool debugLogging = false;

        [SerializeField, Tooltip("Did the last interaction attempt succeed?")]
        private bool lastInteractSucceeded;

        [SerializeField, Tooltip("Realtime since startup of the last interaction attempt.")]
        private float lastInteractTime = -999f;

        private const string SystemTag = "PlayerInteractor";

        // --------------------------------------------------------------------
        // Lifecycle
        // --------------------------------------------------------------------

        protected override void Awake()
        {
            base.Awake();

            if (!mover)
            {
                mover = GetComponent<PlayerMover2D>();
            }
        }

        private void Update()
        {
            // Basic pattern: if the player presses the interaction key,
            // attempt to interact with the cached currentTarget maintained
            // by InteractorBase.
            if (Input.GetKeyDown(primaryInteractKey))
            {
                bool ok = TryInteract();
                lastInteractSucceeded = ok;
                lastInteractTime = Time.realtimeSinceStartup;
            }

            // If you need per-frame UI prompts, you can read
            // BuildGateInfo(currentTarget) from elsewhere (e.g., HUD script)
            // to display "Press E" hints.
        }

        // --------------------------------------------------------------------
        // InteractorBase abstract hooks
        // --------------------------------------------------------------------

        /// <summary>
        /// World position of the player for distance checks.
        /// </summary>
        protected override Vector3 GetOrigin()
        {
            return transform.position;
        }

        /// <summary>
        /// World-space facing vector used for facing-dot gating.
        /// </summary>
        protected override Vector3 GetFacingDirection()
        {
            Vector2 facing2D;

            if (mover != null && mover.Facing.sqrMagnitude > 1e-6f)
            {
                facing2D = mover.Facing.normalized;
            }
            else
            {
                facing2D = idleFacing2D;
            }

            return new Vector3(facing2D.x, facing2D.y, 0f);
        }

        // --------------------------------------------------------------------
        // Optional interaction info passthrough
        // --------------------------------------------------------------------

        /// <summary>
        /// Convenience passthrough so existing code that calls
        /// PlayerInteractor.BuildGateInfo(target) continues to work.
        /// </summary>
        public override InteractionGateInfo BuildGateInfo(InteractableBase target) 
        {
            // Assumes InteractorBase implements BuildGateInfo; if the base
            // signature changes, update this wrapper accordingly.
            return base.BuildGateInfo(target);
        }

        // --------------------------------------------------------------------
        // Logging hooks
        // --------------------------------------------------------------------

        protected override void OnInteractionBlocked(
            InteractableBase target,
            bool inRangeLocal,
            bool facingOkLocal,
            float distance,
            float dot)
        {
            if (!debugLogging)
                return;

            string resultStr;
            if (!inRangeLocal && !facingOkLocal)
                resultStr = "BlockedDistanceAndFacing";
            else if (!inRangeLocal)
                resultStr = "BlockedDistance";
            else if (!facingOkLocal)
                resultStr = "BlockedFacing";
            else
                resultStr = "BlockedOther";

            string targetId = target ? (target.InteractableId ?? target.name) : "<none>";
            string msg =
                $"target={targetId}, dist={distance:F2}, dot={dot:F2}, " +
                $"threshold={interactFacingDotThreshold:F2}, maxDist={interactMaxDistance:F2}";

            GameLog.Log(
                this,
                system: SystemTag,
                action: "TryInteract",
                result: resultStr,
                message: msg);
        }

        protected override void OnInteractionPerformed(
            InteractableBase target,
            bool success,
            float distance,
            float dot)
        {
            if (!debugLogging || target == null)
                return;

            string targetId = target.InteractableId ?? target.name;
            string msg = $"target={targetId}, dist={distance:F2}, dot={dot:F2}";

            GameLog.Log(
                this,
                system: SystemTag,
                action: "TryInteract",
                result: success ? "Success" : "Failed",
                message: msg);
        }

        // --------------------------------------------------------------------
        // Gizmos
        // --------------------------------------------------------------------

        private void OnDrawGizmosSelected()
        {
#if UNITY_EDITOR
            // Simple gizmo: draw a line from the player to the current target,
            // colored by whether the last gating result (canInteract) was true.
            if (!Application.isPlaying)
                return;

            if (currentTarget == null)
                return;

            Vector3 origin = transform.position;
            Vector3 targetPos = currentTarget.transform.position;

            Gizmos.color = canInteract ? Color.green : Color.red;
            Gizmos.DrawLine(origin, targetPos);
            Gizmos.DrawSphere(targetPos, 0.05f);
#endif
        }
    }
}
