using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorldGrid.Runtime.Tiles
{
    [Serializable]
    public struct TileLibraryKey : IEquatable<TileLibraryKey>
    {
        [SerializeField]
        private string value;

        public string Value => value ?? string.Empty;
        public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

        public bool Equals(TileLibraryKey other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is TileLibraryKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            // Matches Equals() which uses Ordinal comparison.
            return StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value;
        }
    }
}
