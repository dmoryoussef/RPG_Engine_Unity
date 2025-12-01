using System.Collections.Generic;
using UnityEngine;

namespace Interaction
{
    /// <summary>
    /// Central index for all InteractableBase instances at runtime.
    ///
    /// Interactors use this instead of FindObjectsOfType.
    /// Later, this can delegate to a WorldIndex without changing call sites.
    /// </summary>
    public static class InteractableRegistry
    {
        private static readonly List<InteractableBase> _all =
            new List<InteractableBase>(128);

        /// <summary>
        /// Read-only snapshot of all currently registered interactables.
        /// </summary>
        public static IReadOnlyList<InteractableBase> All => _all;

        public static void Register(InteractableBase interactable)
        {
            if (interactable == null)
            {
                return;
            }

            if (!_all.Contains(interactable))
            {
                _all.Add(interactable);
            }
        }

        public static void Unregister(InteractableBase interactable)
        {
            if (interactable == null)
            {
                return;
            }

            _all.Remove(interactable);
        }

#if UNITY_EDITOR
        // Make sure we don't keep stale references across domain reloads in editor.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearOnDomainReload()
        {
            _all.Clear();
        }
#endif
    }
}
