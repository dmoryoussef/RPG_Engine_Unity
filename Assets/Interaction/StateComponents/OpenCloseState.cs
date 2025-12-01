using System;
using UnityEngine;

namespace Interaction
{
    public enum OpenCloseAction { Toggle, Open, Close }
    public enum StateChangeResult
    {
        Opened,
        Closed,
        AlreadyOpen,
        AlreadyClosed,
        FailedLocked,
        Failed
    }

    /// <summary>
    /// Owns the door/chest/etc. state. Optionally observes a LockState to guard changes.
    /// Responsible for: state value, collider blocking, animation, and change events.
    /// </summary>
    [DisallowMultipleComponent]
    public class OpenCloseState : MonoBehaviour
    {
        [Header("State")]
        [SerializeField] private bool _isOpen = false;
        public bool IsOpen => _isOpen;

        [Header("Optional Lock")]
        [Tooltip("If assigned/found, changing state will fail while locked.")]
        [SerializeField] private LockState _lockState; // optional reference
        public bool IsLocked => _lockState && _lockState.IsLocked;

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

        /// <summary>Fired only when the open/closed value actually changes.</summary>
        public event Action<bool /*oldIsOpen*/, bool /*newIsOpen*/> OnStateChanged;

        private void Awake()
        {
            // Be resilient: try to auto-wire a LockState if not manually assigned.
            if (!_lockState)
                _lockState = GetComponent<LockState>() ?? GetComponentInParent<LockState>(true);

            EnsureBlockingCollider();
            EnsureAnimator();
            ApplyBlocking();
            ApplyAnimation();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!_lockState)
                _lockState = GetComponent<LockState>() ?? GetComponentInParent<LockState>(true);

            EnsureBlockingCollider();
            EnsureAnimator();
            ApplyBlocking();
            ApplyAnimation();
        }
#endif

        // ------------------- Single public surface -------------------

        /// <summary>
        /// Request a state change. Returns a detailed result.
        /// Lock checks are internal and will return FailedLocked when applicable.
        /// </summary>
        public StateChangeResult TryStateChange(OpenCloseAction action)
        {
            if (IsLocked) return StateChangeResult.FailedLocked;

            bool desiredOpen = action switch
            {
                OpenCloseAction.Toggle => !_isOpen,
                OpenCloseAction.Open => true,
                OpenCloseAction.Close => false,
                _ => _isOpen
            };

            if (desiredOpen == _isOpen)
                return _isOpen ? StateChangeResult.AlreadyOpen : StateChangeResult.AlreadyClosed;

            bool old = _isOpen;
            _isOpen = desiredOpen;

            // Side-effects on real changes
            ApplyBlocking();
            ApplyAnimation();
            OnStateChanged?.Invoke(old, _isOpen);

            return _isOpen ? StateChangeResult.Opened : StateChangeResult.Closed;
        }

        // ------------------- Internals -------------------

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
            if (any && !any.isTrigger) { _blockingCollider = any; return; }

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

        // -------- Optional helpers for wiring (if needed by spawners/editors) --------
        public void SetLockState(LockState lockState) => _lockState = lockState;
    }
}
