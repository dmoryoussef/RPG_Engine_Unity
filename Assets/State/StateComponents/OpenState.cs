using System;
using UnityEngine;

namespace State
{
    public enum OpenCloseAction { Toggle, Open, Close }

    public enum OpenCloseResult
    {
        Opened,
        Closed,
        AlreadyOpen,
        AlreadyClosed,
        Failed,        // generic failure
        FailedBlocked, // blocked (non-lock or unknown reason)
        FailedLocked   // specifically blocked by "locked"
    }

    /// <summary>
    /// Owns the door/chest/etc. open/closed state.
    /// Handles collider blocking, animation, and change events.
    /// Blockers are configured via BaseState._blockingStates.
    /// </summary>
    [DisallowMultipleComponent]
    public class OpenCloseState : BaseState
    {
        [Header("State")]
        [SerializeField] private bool _isOpen = false;
        public bool IsOpen => _isOpen;

        [Header("Blocking (auto-setup)")]
        [Tooltip("Enabled when CLOSED, disabled when OPEN. Auto-created if missing.")]
        [SerializeField] private Collider2D _blockingCollider;
        [SerializeField] private bool _manageBlockingCollider = true;
        [SerializeField] private bool _autoCreateBlockingCollider = true;

        public Collider2D BlockingCollider => _blockingCollider;

        [Header("Animation (optional)")]
        [Tooltip("Animator with a bool parameter named 'IsOpen'. Auto-found if missing.")]
        [SerializeField] private Animator _animator;
        [SerializeField] private string _animIsOpenParam = "IsOpen";

        [Header("Description")]
        [SerializeField] private string _descriptionCategory = "Door";
        [SerializeField] private int _descriptionPriority = 10;

        /// <summary>
        /// Fired only when the open/closed value actually changes (old, new).
        /// Use this for domain-specific responses.
        /// </summary>
        public event Action<bool /*oldIsOpen*/, bool /*newIsOpen*/> OnIsOpenChanged;

        private void Awake()
        {
            EnsureBlockingCollider();
            EnsureAnimator();
            ApplyBlocking();
            ApplyAnimation();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsureBlockingCollider();
            EnsureAnimator();
            ApplyBlocking();
            ApplyAnimation();
        }
#endif

        // ---------------- Domain-level API (used by scripts/quests/etc.) ----------------

        /// <summary>
        /// Request a state change with a specific action.
        /// Block checks are handled by BaseState.CheckBlockers().
        /// </summary>
        public OpenCloseResult TryStateChange(OpenCloseAction action)
        {
            // Generic blocking hook via BaseState.
            var block = CheckBlockers();
            // TODO:: only block with lock if door is closed
            if (!block.IsSuccess)
            {
                // If any blocker reported the canonical "locked" reason key,
                // preserve that as a distinct enum value for UX / quest logic.
                if (block.Status == StateStatus.Blocked && block.Message == "locked")
                    return OpenCloseResult.FailedLocked;

                return OpenCloseResult.FailedBlocked;
            }

            bool desiredOpen = action switch
            {
                OpenCloseAction.Toggle => !_isOpen,
                OpenCloseAction.Open => true,
                OpenCloseAction.Close => false,
                _ => _isOpen
            };

            if (desiredOpen == _isOpen)
                return _isOpen ? OpenCloseResult.AlreadyOpen : OpenCloseResult.AlreadyClosed;

            bool old = _isOpen;
            _isOpen = desiredOpen;

            ApplyBlocking();
            ApplyAnimation();

            // Domain-specific detailed event
            OnIsOpenChanged?.Invoke(old, _isOpen);

            // Generic BaseState “something changed” hook (for inspection, etc.)
            NotifyStateChanged();

            return _isOpen ? OpenCloseResult.Opened : OpenCloseResult.Closed;
        }

        // --------------- Interaction-facing API (for InteractableComponent) ---------------

        /// <summary>
        /// Default "interact" behavior for this state (e.g., press E on the door).
        /// Wraps the domain API in a generic StateResult.
        /// </summary>
        public override StateResult TryStateChange()
        {
            var domainResult = TryStateChange(OpenCloseAction.Toggle);

            var generic = domainResult switch
            {
                OpenCloseResult.Opened =>
                    StateResult.Succeed("opened"),

                OpenCloseResult.Closed =>
                    StateResult.Succeed("closed"),

                OpenCloseResult.AlreadyOpen or OpenCloseResult.AlreadyClosed =>
                    StateResult.AlreadyInState("already_in_state"),

                OpenCloseResult.FailedLocked =>
                    StateResult.Blocked("locked"),

                OpenCloseResult.FailedBlocked =>
                    StateResult.Blocked("blocked"),

                _ =>
                    StateResult.Fail("failed")
            };

            return Report(generic);
        }

        // ---------------- Description (used by PanelContributorComponent) ----------------

        public override string GetDescriptionText()
            => IsOpen ? "Open" : "Closed";

        public override int GetDescriptionPriority()
            => _descriptionPriority;

        public override string GetDescriptionCategory()
            => _descriptionCategory;

        // ---------------- Internals ----------------

        private void ApplyBlocking()
        {
            if (!_manageBlockingCollider || !_blockingCollider) return;
            _blockingCollider.enabled = !_isOpen; // solid when closed
        }

        private void EnsureBlockingCollider()
        {
            if (_blockingCollider && _blockingCollider.gameObject == gameObject)
            {
                _blockingCollider.isTrigger = false;
                return;
            }

            if (!_autoCreateBlockingCollider) return;

            var any = GetComponent<Collider2D>();
            if (any && !any.isTrigger)
            {
                _blockingCollider = any;
                return;
            }

            var box = GetComponent<BoxCollider2D>();
            if (!box) box = gameObject.AddComponent<BoxCollider2D>();
            box.isTrigger = false;
            FitBoxToSprite(box);
            _blockingCollider = box;
        }

        private void EnsureAnimator()
        {
            if (_animator) return;
            _animator = GetComponent<Animator>();
        }

        private void ApplyAnimation()
        {
            if (!_animator) return;
            _animator.SetBool(_animIsOpenParam, _isOpen);
        }

        private void FitBoxToSprite(BoxCollider2D box)
        {
            var sr = GetComponent<SpriteRenderer>();
            if (!sr || !sr.sprite) return;
            var b = sr.sprite.bounds;
            box.size = b.size;
            box.offset = b.center;
        }
    }
}
