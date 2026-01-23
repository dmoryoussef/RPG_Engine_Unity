using System.Collections.Generic;
using UnityEngine;

namespace WorldGrid.Runtime.Tiles
{
    public sealed class TileLibrary
    {
        #region State

        private readonly Dictionary<int, TileDef> _defs;
        private readonly int _defaultTileId;

        #endregion

        #region Construction

        public TileLibrary(Dictionary<int, TileDef> defs, int defaultTileId)
        {
            _defs = defs ?? new Dictionary<int, TileDef>();
            _defaultTileId = defaultTileId;
        }

        #endregion

        #region Lookup

        public bool TryGetDef(int tileId, out TileDef def)
        {
            if (tileId == _defaultTileId)
            {
                def = null;
                return false;
            }

            return _defs.TryGetValue(tileId, out def);
        }

        public IEnumerable<TileDef> EnumerateDefs()
        {
            return _defs.Values;
        }

        #endregion

        #region UV

        public bool TryGetUv(int tileId, out RectUv uv)
        {
            uv = default;

            if (!TryGetDef(tileId, out var def))
                return false;

            uv = def.Uv;
            return true;
        }

        #endregion

        #region Color / Properties

        public bool TryGetColor(int tileId, out Color32 color)
        {
            color = new Color32(255, 255, 255, 255);

            if (!TryGetProperty<TileColorProperty>(tileId, out var prop))
                return false;

            color = prop.Color;
            return true;
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

        public bool TryGetProperty<T>(int tileId, out T property) where T : TileProperty
        {
            property = null;

            if (!TryGetDef(tileId, out var def))
                return false;

            return def.TryGetProperty(out property);
        }

        #endregion

        public string ToDebugString(int maxEntries = 64)
        {
            var sb = new System.Text.StringBuilder(256);

            sb.Append("TileLibrary { ");
            sb.Append("defaultTileId=").Append(_defaultTileId);
            sb.Append(", tileCount=").Append(_defs != null ? _defs.Count : 0);

            if (_defs == null || _defs.Count == 0)
            {
                sb.Append(" }");
                return sb.ToString();
            }

            sb.Append(", tiles=[");

            int shown = 0;
            foreach (var kvp in _defs)
            {
                if (shown > 0)
                    sb.Append(", ");

                int id = kvp.Key;
                var def = kvp.Value;

                sb.Append(id);

                // Include name when available for easier debugging.
                if (def != null && !string.IsNullOrEmpty(def.Name))
                    sb.Append(":").Append(def.Name);

                shown++;
                if (shown >= maxEntries)
                    break;
            }

            if (_defs.Count > shown)
                sb.Append(", ...");

            sb.Append("] }");
            return sb.ToString();
        }

    }
}
