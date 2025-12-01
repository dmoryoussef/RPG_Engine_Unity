using System;
using UnityEngine;

namespace Interaction
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

    /// <summary>
    /// Generic lock state container usable by any lockable object (door, chest, switch, etc.).
    /// Single entry point: TryStateChange(LockAction) → LockResult.
    /// Optional: drives an Animator bool parameter ("IsLocked") when state changes.
    /// </summary>
    [DisallowMultipleComponent]
    public class LockState : MonoBehaviour
    {
        [Header("State")]
        [SerializeField] private bool _isLocked = false;
        public bool IsLocked => _isLocked;

        [Header("Animation (optional)")]
        [SerializeField] private Animator _animator;
        [SerializeField] private string _animIsLockedParam = "IsLocked";

        /// <summary>Fired only when lock value actually changes.</summary>
        public event Action<bool /*oldIsLocked*/, bool /*newIsLocked*/> OnStateChanged;

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

        // ------------------- Single public surface -------------------

        public LockResult TryStateChange(LockAction action)
        {
            bool desiredLocked = action switch
            {
                LockAction.Toggle => !_isLocked,
                LockAction.Lock => true,
                LockAction.Unlock => false,
                _ => _isLocked
            };

            if (desiredLocked == _isLocked)
                return _isLocked ? LockResult.AlreadyLocked : LockResult.AlreadyUnlocked;

            bool old = _isLocked;
            _isLocked = desiredLocked;

            ApplyAnimation();
            OnStateChanged?.Invoke(old, _isLocked);

            return _isLocked ? LockResult.Locked : LockResult.Unlocked;
        }

        // ------------------- Internals -------------------

        private void EnsureAnimator()
        {
            if (_animator) return;
            _animator = GetComponent<Animator>();
        }

        private void ApplyAnimation()
        {
            if (!_animator) return;
            _animator.SetBool(_animIsLockedParam, _isLocked);
        }
    }
}
