using System;
using UnityEngine;

namespace State
{
    public enum LockAction { Toggle, Lock, Unlock }

    public enum LockResult
    {
        Locked,
        Unlocked,
        AlreadyLocked,
        AlreadyUnlocked,
        Failed
    }

    [DisallowMultipleComponent]
    public class LockState : BaseState
    {
        [Header("State")]
        [SerializeField] private bool _isLocked = false;
        public bool IsLocked => _isLocked;

        [Header("Animation (optional)")]
        [SerializeField] private Animator _animator;
        [SerializeField] private string _animIsLockedParam = "IsLocked";

        [Header("Description")]
        [SerializeField] private string _descriptionCategory = "Door";
        [SerializeField] private int _descriptionPriority = 10;

        /// <summary>
        /// Fired only when lock value actually changes (old, new).
        /// Use this for domain-specific reactions.
        /// </summary>
        public event Action<bool /*oldIsLocked*/, bool /*newIsLocked*/> OnIsLockedChanged;

        private void Awake()
        {
            EnsureAnimator();
            ApplyAnimation();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsureAnimator();
            ApplyAnimation();
        }
#endif

        // ---------------- Domain API ----------------

        /// <summary>
        /// Domain-level lock/unlock API.
        /// Scripts/quests/AI should call this overload.
        /// </summary>
        public LockResult TryStateChange(LockAction action)
        {
            bool desired = action switch
            {
                LockAction.Toggle => !_isLocked,
                LockAction.Lock => true,
                LockAction.Unlock => false,
                _ => _isLocked
            };

            if (desired == _isLocked)
                return _isLocked ? LockResult.AlreadyLocked : LockResult.AlreadyUnlocked;

            bool old = _isLocked;
            _isLocked = desired;

            ApplyAnimation();

            // Domain-specific detailed event
            OnIsLockedChanged?.Invoke(old, _isLocked);

            // Generic BaseState “something changed” hook (inspection, etc.)
            NotifyStateChanged();

            return _isLocked ? LockResult.Locked : LockResult.Unlocked;
        }

        // ------------- Interaction-facing -------------

        /// <summary>
        /// Default "interact" behavior for this state (e.g., press E on the lock).
        /// Wraps the domain API in a generic StateResult for the interaction system.
        /// </summary>
        public override StateResult TryStateChange(StateChangeContext context)
        {
            var domainResult = TryStateChange(LockAction.Toggle);

            var generic = domainResult switch
            {
                LockResult.Locked =>
                    StateResult.Succeed("locked"),

                LockResult.Unlocked =>
                    StateResult.Succeed("unlocked"),

                LockResult.AlreadyLocked or LockResult.AlreadyUnlocked =>
                    StateResult.AlreadyInState("already_in_state"),

                _ =>
                    StateResult.Fail("failed")
            };

            return Report(generic);
        }

        // ---------------- Blocking ----------------

        /// <summary>
        /// While locked, this state blocks any states that reference it in their _blockingStates list.
        /// Reason key: "locked".
        /// </summary>
        public override bool IsBlocking(BaseState target, out string reasonKey)
        {
            if (_isLocked)
            {
                reasonKey = "locked";
                return true;
            }

            reasonKey = null;
            return false;
        }

        // ---------------- Description (for PanelContributorComponent) ----------------

        public override string GetDescriptionText()
            => IsLocked ? "Locked" : "Unlocked";

        public override int GetDescriptionPriority()
            => _descriptionPriority;

        public override string GetDescriptionCategory()
            => _descriptionCategory;

        // ---------------- Internals ----------------

        private void EnsureAnimator()
        {
            if (_animator) return;
            _animator = GetComponent<Animator>();
        }

        private void ApplyAnimation()
        {
            if (!_animator) return;
            //_animator.SetBool(_animIsLockedParam, _isLocked);
        }
    }
}
