using UnityEngine;
using RPG.Foundation;

namespace RPG.World
{
    /// <summary>
    /// General interactable that locks or unlocks a target LockState.
    /// Works for doors, chests, switches, etc.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public class LockStateInteractable : InteractableBase
    {
        [SerializeField] private LockState targetLockState;
        [SerializeField] private bool setLocked = true; // true = lock, false = unlock

        protected override void Awake()
        {
            base.Awake();
            if (!targetLockState)
                targetLockState = GetComponent<LockState>() ?? GetComponentInParent<LockState>();
        }

#if UNITY_EDITOR
        protected override void ValidateExtra()
        {
            if (!targetLockState)
                Debug.Log($"<color=red>[LockStateInteractable]</color> Missing LockState reference on '{name}'.");
        }
#endif

        protected override bool DoInteract()
        {
            if (!targetLockState)
                return false;

            bool changed = setLocked ? targetLockState.Lock() : targetLockState.Unlock();

            if (changed)
                Debug.Log(setLocked ? "Locked." : "Unlocked.");
            else
                Debug.Log(setLocked ? "Already locked." : "Already unlocked.");

            return changed;
        }
    }
}
