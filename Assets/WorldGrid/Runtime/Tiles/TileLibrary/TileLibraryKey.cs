using System;
using UnityEngine;

namespace WorldGrid.Runtime.Tiles
{
    /// <summary>
    /// Typed string key used to identify a tile library ("world", "debug", "interior", etc.).
    /// Keeps consumer APIs stable even if the underlying identifier strategy changes later.
    /// </summary>
    [Serializable]
    public struct TileLibraryKey : IEquatable<TileLibraryKey>
    {
        [SerializeField] private string value;

        public string Value => value ?? string.Empty;

        public TileLibraryKey(string value)
        {
            this.value = value ?? string.Empty;
        }

        public bool IsEmpty => string.IsNullOrEmpty(Value);

        public bool Equals(TileLibraryKey other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is TileLibraryKey other && Equals(other);

        public override int GetHashCode() => (Value ?? string.Empty).GetHashCode();

        public override string ToString() => Value;

        public static implicit operator TileLibraryKey(string v) => new TileLibraryKey(v);
    }
}
