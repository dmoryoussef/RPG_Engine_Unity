using UnityEngine;

namespace RPG.Foundation
{
    /// <summary>
    /// Generic lock state container usable by any lockable object (door, chest, switch, etc.).
    /// Simply tracks whether it's locked and provides methods to toggle it.
    /// </summary>
    [DisallowMultipleComponent]
    public class LockState : MonoBehaviour
    {
        [Header("State")]
        [SerializeField] private bool isLocked = false;
        public bool IsLocked => isLocked;

        public bool Lock()
        {
            if (isLocked) return false;
            isLocked = true;
            return true;
        }

        public bool Unlock()
        {
            if (!isLocked) return false;
            isLocked = false;
            return true;
        }

        public bool Toggle()
        {
            isLocked = !isLocked;
            return true;
        }
    }
}
