using System;
using System.Collections.Generic;

namespace WorldGrid.Runtime.Tiles
{
    /// <summary>
    /// Maps tileId -> tile definition (UV rect, name, tags).
    /// Pure runtime data; Unity adapters should populate this.
    /// </summary>
    public sealed class TileLibrary
    {
        private readonly Dictionary<int, TileDef> _defs = new();

        public int Count => _defs.Count;

        public void Clear() => _defs.Clear();

        /// <summary>
        /// Adds or replaces a tile definition for tileId.
        /// </summary>
        public void Set(TileDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            _defs[def.TileId] = def;
        }

        public bool TryGet(int tileId, out TileDef def) =>
            _defs.TryGetValue(tileId, out def);

        public bool TryGetUv(int tileId, out RectUv uv)
        {
            if (_defs.TryGetValue(tileId, out var def))
            {
                uv = def.Uv;
                return true;
            }

            uv = default;
            return false;
        }

        public IReadOnlyList<string> GetTagsOrEmpty(int tileId)
        {
            if (_defs.TryGetValue(tileId, out var def))
                return def.Tags;

            return Array.Empty<string>();
        }

        /// <summary>
        /// Debug string for a tileId lookup (since ToString() can’t accept parameters).
        /// </summary>
        public string ToString(int tileId)
        {
            if (_defs.TryGetValue(tileId, out var def))
                return def.ToString();

            return $"<unknown tileId={tileId}>";
        }
    }
}
