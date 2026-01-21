using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorldGrid.Runtime.Tiles
{
    /// <summary>
    /// Runtime lookup table: tileId -> TileDef.
    /// Also provides a generic property query API so systems do not need hard-coded getters.
    /// </summary>
    public sealed class TileLibrary
    {
        private readonly Dictionary<int, TileDef> _defs = new();

        public int Count => _defs.Count;
        public IEnumerable<TileDef> EnumerateDefs() => _defs.Values;

        public void Clear()
        {
            _defs.Clear();
        }

        /// <summary>
        /// Adds or replaces a tile definition for its tileId.
        /// </summary>
        public void Set(TileDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            _defs[def.TileId] = def;
        }

        /// <summary>
        /// Optional hook for post-population validation later.
        /// Currently a no-op by design.
        /// </summary>
        public void FinalizeBuild()
        {
            // Intentionally empty for now.
            // Future examples: validate duplicate ids, missing ids, etc.
        }

        public bool TryGetDef(int tileId, out TileDef def) =>
            _defs.TryGetValue(tileId, out def);

        /// <summary>
        /// Generic property lookup: asks for a property type on the tile definition for tileId.
        /// This is Option 2’s key API.
        /// </summary>
        public bool TryGetProperty<T>(int tileId, out T property)
            where T : TileProperty
        {
            if (_defs.TryGetValue(tileId, out var def))
            {
                return def.TryGetProperty(out property);
            }

            property = null;
            return false;
        }

        /// <summary>
        /// Renderer helper: try get UV for a tileId.
        /// </summary>
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

        /// <summary>
        /// Renderer helper: try get base color for a tileId.
        /// Defaults to opaque white if tileId is unknown or has no color property.
        /// </summary>
        public bool TryGetColor(int tileId, out Color32 color)
        {
            if (TryGetProperty<TileColorProperty>(tileId, out var colorProp) && colorProp != null)
            {
                color = colorProp.Color;
                return true;
            }

            // Unknown tileId or missing property => white.
            color = new Color32(255, 255, 255, 255);
            return false;
        }

        /// <summary>
        /// Renderer helper: try get per-cell jitter amplitude (0..0.25 recommended).
        /// Defaults to 0 if tileId is unknown or has no color property.
        /// </summary>
        public bool TryGetColorJitter(int tileId, out float jitter)
        {
            if (TryGetProperty<TileColorProperty>(tileId, out var colorProp) && colorProp != null)
            {
                jitter = colorProp.Jitter;
                return true;
            }

            jitter = 0f;
            return false;
        }

        /// <summary>
        /// Debug/tools helper: returns tags or an empty list if tileId is unknown.
        /// </summary>
        public IReadOnlyList<string> GetTagsOrEmpty(int tileId)
        {
            if (_defs.TryGetValue(tileId, out var def))
                return def.Tags;

            return Array.Empty<string>();
        }

        /// <summary>
        /// Debug string for a tileId lookup.
        /// </summary>
        public string ToDebugString(int tileId)
        {
            if (_defs.TryGetValue(tileId, out var def))
                return def.ToString();

            return $"<unknown tileId={tileId}>";
        }
    }
}
