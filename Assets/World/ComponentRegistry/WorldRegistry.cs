using System;
using System.Collections.Generic;

namespace World
{
    /// <summary>
    /// Global, type-safe registry for gameplay objects/components.
    /// 
    /// Key ideas:
    /// - Dictionary<Type, List<object>> so each interface/base has its own list.
    /// - Register/Unregister are generic: WorldRegistry.Register<IInteractable>(this).
    /// - GetAll<T>() gives a read-only view.
    /// - GetAllNonAlloc<T>() fills a caller-provided list for hot paths.
    /// 
    /// Assumes calls happen on Unity main thread (no locking).
    /// </summary>
    public static class Registry
    {
        private static readonly Dictionary<Type, List<object>> _byType = new();

        /// <summary>
        /// Registers an instance under the generic type key T.
        /// Common pattern:
        ///   WorldRegistry.Register<IInteractable>(this);
        ///   WorldRegistry.Register<InteractableBase>(this);
        /// </summary>
        public static void Register<T>(T instance) where T : class
        {
            if (instance == null) return;

            var type = typeof(T);

            if (!_byType.TryGetValue(type, out var list))
            {
                list = new List<object>(64);
                _byType[type] = list;
            }

            // Guard against duplicate registration.
            if (!list.Contains(instance))
            {
                list.Add(instance);
            }
        }

        /// <summary>
        /// Unregisters an instance from the list keyed by T.
        /// Safe to call multiple times; does nothing if not present.
        /// </summary>
        public static void Unregister<T>(T instance) where T : class
        {
            if (instance == null) return;

            var type = typeof(T);

            if (!_byType.TryGetValue(type, out var list))
                return;

            list.Remove(instance);
        }

        /// <summary>
        /// Returns a snapshot of all instances registered under T.
        /// This allocates a new array-backed list, so avoid in tight loops.
        /// </summary>
        public static IReadOnlyList<T> GetAll<T>() where T : class
        {
            var type = typeof(T);

            if (!_byType.TryGetValue(type, out var list) || list.Count == 0)
                return Array.Empty<T>();

            var result = new List<T>(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is T typed && typed != null)
                    result.Add(typed);
            }

            return result;
        }

        /// <summary>
        /// Non-alloc variant: fills caller-provided list with current instances of T.
        /// Clears the list first.
        /// </summary>
        public static void GetAllNonAlloc<T>(List<T> results) where T : class
        {
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            results.Clear();

            var type = typeof(T);

            if (!_byType.TryGetValue(type, out var list) || list.Count == 0)
                return;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is T typed && typed != null)
                    results.Add(typed);
            }
        }

        /// <summary>
        /// Optional: clear everything (e.g., between scenes or for tests).
        /// </summary>
        public static void ClearAll()
        {
            _byType.Clear();
        }

        /// <summary>
        /// Optional: clear only a specific type.
        /// </summary>
        public static void ClearAllOfType<T>() where T : class
        {
            var type = typeof(T);
            _byType.Remove(type);
        }
    }
}
