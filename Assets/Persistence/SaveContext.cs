using System;
using System.Collections.Generic;

namespace Persistence
{
    /// <summary>
    /// A type-safe bag of references for systems participating in persistence.
    /// Sections can Get/Set objects without a monolithic SaveGame class.
    /// </summary>
    public sealed class SaveContext
    {
        private readonly Dictionary<Type, object> _map = new();

        public void Set<T>(T obj) where T : class
            => _map[typeof(T)] = obj ?? throw new ArgumentNullException(nameof(obj));

        public T Get<T>() where T : class
            => (T)_map[typeof(T)];

        public bool TryGet<T>(out T obj) where T : class
        {
            if (_map.TryGetValue(typeof(T), out var o))
            {
                obj = (T)o;
                return true;
            }

            obj = null;
            return false;
        }

        public bool Remove<T>() where T : class
            => _map.Remove(typeof(T));

        public void Clear() => _map.Clear();
    }
}
