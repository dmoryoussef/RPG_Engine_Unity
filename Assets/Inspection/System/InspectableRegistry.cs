using System.Collections.Generic;
using UnityEngine;

namespace Inspection
{
    /// <summary>
    /// Central index for all runtime InspectableComponent instances.
    ///
    /// - Inspection systems use this instead of FindObjectsOfType.
    /// - Later, this can forward to a generic WorldIndex without changing call sites.
    /// </summary>
    public static class InspectableRegistry
    {
        private static readonly List<InspectableComponent> _all =
            new List<InspectableComponent>(128);

        /// <summary>
        /// Read-only snapshot of all currently registered inspectables.
        /// </summary>
        public static IReadOnlyList<InspectableComponent> All => _all;

        /// <summary>
        /// Register an inspectable instance.
        /// Called from InspectableComponent.OnEnable().
        /// </summary>
        public static void Register(InspectableComponent inspectable)
        {
            if (inspectable == null)
            {
                return;
            }

            if (!_all.Contains(inspectable))
            {
                _all.Add(inspectable);
            }
        }

        /// <summary>
        /// Unregister an inspectable instance.
        /// Called from InspectableComponent.OnDisable().
        /// </summary>
        public static void Unregister(InspectableComponent inspectable)
        {
            if (inspectable == null)
            {
                return;
            }

            _all.Remove(inspectable);
        }

#if UNITY_EDITOR
        // Ensures a clean list when domain reloads in the editor.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearOnDomainReload()
        {
            _all.Clear();
        }
#endif
    }
}
