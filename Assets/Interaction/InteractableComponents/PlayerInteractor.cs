using System.Collections.Generic;
using Logging;
using Player;
using UnityEngine;

namespace Interaction
{
    /// <summary>
    /// Player-specific interactor built on top of InteractorBase.
    ///
    /// Responsibilities:
    /// - Provide player origin/facing for the base gating logic.
    /// - Use the cached currentTarget from InteractorBase
    ///   (fed by TargeterComponent via InteractorBase’s own wiring).
    /// - Support multiple InteractableBase components on the same root,
    ///   each with its own InteractionKey (E = open/close, L = lock, etc.).
    /// - Optionally log interaction attempts/results via GameLog.
    ///
    /// It does NOT:
    /// - Do its own target selection.
    /// - Re-wire TargeterComponent events.
    /// </summary>
    public sealed class PlayerInteractor : InteractorBase
    {
        [Header("Dependencies")]
        [SerializeField] private PlayerMover2D _mover;

        [Header("Debug")]
        [SerializeField, Tooltip("If true, interaction attempts will be logged via GameLog.")]
        private bool _debugLogging = false;

        [SerializeField, Tooltip("Did the last interaction attempt succeed?")]
        private bool _lastInteractSucceeded;

        [SerializeField, Tooltip("Realtime since startup of the last interaction attempt.")]
        private float _lastInteractTime = -999f;

        private const string SystemTag = "PlayerInteractor";

        // Scratch list to avoid allocations when gathering interactables on the root.
        private readonly List<InteractableComponent> _interactablesOnRoot =
            new List<InteractableComponent>(8);

        // --------------------------------------------------------------------
        // Lifecycle
        // --------------------------------------------------------------------

        protected override void Awake()
        {
            base.Awake();

            if (!_mover)
            {
                _mover = GetComponent<PlayerMover2D>();
            }
        }

        private void Update()
        {
            base.Update();

            // We rely entirely on InteractorBase's cached currentTarget,
            // which is updated from TargeterComponent.Model.CurrentTargetChanged.
            if (!currentTarget)
                return;

            // Global cancel key: Escape.
            // This sends a "Cancel" channel into the current target's state.
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                currentTarget.OnCancel(this);
                return;
            }

            HandleKeyInteractionsForCurrentRoot();
        }

        // --------------------------------------------------------------------
        // InteractorBase abstract hooks (gating origin/facing)
        // --------------------------------------------------------------------

        protected override Vector3 GetOrigin()
        {
            return transform.position;
        }

        protected override Vector3 GetFacingDirection()
        {
            // Use mover's facing if available and non-zero, otherwise fall back
            // to a default (down) so gating never fails due to a zero vector.
            Vector2 facing2D = Vector2.down;

            if (_mover != null && _mover.Facing.sqrMagnitude > 1e-6f)
            {
                facing2D = _mover.Facing.normalized;
            }

            return new Vector3(facing2D.x, facing2D.y, 0f);
        }

        // --------------------------------------------------------------------
        // Multi-interactable, multi-key behavior
        // --------------------------------------------------------------------

        /// <summary>
        /// Looks at the GameObject of the currentTarget, finds all InteractableBase
        /// components on that root, and checks each one's InteractionKey against input.
        ///
        /// On a key press, we call the base TryInteract(target) to apply distance+facing
        /// gating and then OnInteract() on that specific interactable.
        /// </summary>
        private void HandleKeyInteractionsForCurrentRoot()
        {
            var root = currentTarget.gameObject;

            _interactablesOnRoot.Clear();
            root.GetComponents(_interactablesOnRoot);

            if (_interactablesOnRoot.Count == 0)
            {
                // Safety fallback: treat currentTarget as the only interactable.
                _interactablesOnRoot.Add(currentTarget);
            }

            foreach (var interactable in _interactablesOnRoot)
            {
                if (!interactable)
                    continue;

                var key = interactable.InteractionKey;
                if (key == KeyCode.None)
                    continue;

                if (!Input.GetKeyDown(key))
                    continue;

                // if this isn't the currentTarget, only allow it if the state opted in.
                bool isCurrentTarget = interactable == currentTarget;
                if (!isCurrentTarget && !interactable.AllowStateChangeWhenNotTargeted)
                    continue;

                // Use the base class gating + interaction logic:
                bool ok = TryInteract(interactable);

                _lastInteractSucceeded = ok;
                _lastInteractTime = Time.realtimeSinceStartup;

                if (_debugLogging)
                {
                    var id = interactable.InteractableId ?? interactable.name;
                    GameLog.Log(
                        this,
                        system: SystemTag,
                        action: "KeyInteract",
                        result: ok ? "Success" : "Failed",
                        message: $"key={key}, target={id}");
                }

                // Only fire one interact per frame/key press.
                return;
            }
        }

        // --------------------------------------------------------------------
        // Optional: override base logging hooks to centralize logs
        // --------------------------------------------------------------------

        protected override void OnInteractionBlocked(
            InteractableComponent target,
            bool inRangeLocal,
            bool facingOkLocal,
            float distance,
            float dot)
        {
            if (!_debugLogging || target == null)
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

            string targetId = target.InteractableId ?? target.name;
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
            InteractableComponent target,
            bool success,
            float distance,
            float dot)
        {
            if (!_debugLogging || target == null)
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
    }
}
