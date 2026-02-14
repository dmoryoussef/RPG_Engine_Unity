using System.Collections.Generic;
using UnityEngine;

namespace WorldGrid.Runtime.Tiles
{
    public sealed class TileLibrary
    {
        #region State

        private readonly Dictionary<int, TileDef> _defs;
        private readonly int _defaultTileId;

        private readonly Color32[] _baseColors;
        private readonly float[] _baseColorInfluences;

        /// <summary>
        /// Compiled runtime channels derived from this library.
        /// </summary>
        public TileChannels Channels { get; private set; }

        #endregion

        #region Construction

        public TileLibrary(Dictionary<int, TileDef> defs, int defaultTileId)
        {
            _defs = defs ?? new Dictionary<int, TileDef>();
            _defaultTileId = defaultTileId;

            // Build compiled channels immediately so consumers always have them.
            // IMPORTANT: derive max tileId from keys (not count) to support sparse/non-contiguous IDs.
            int maxTileId = computeMaxTileIdKey();

            _baseColors = buildBaseColorChannel(maxTileId);
            _baseColorInfluences = buildBaseColorInfluenceChannel(maxTileId);

            Channels = new TileChannels(this, maxTileId);
        }

        private int computeMaxTileIdKey()
        {
            int maxId = 0;

            if (_defs == null || _defs.Count == 0)
                return maxId;

            foreach (var kvp in _defs)
            {
                int id = kvp.Key;
                if (id == _defaultTileId)
                    continue;

                if (id > maxId)
                    maxId = id;
            }

            return maxId;
        }

        private Color32[] buildBaseColorChannel(int maxTileId)
        {
            int len = Mathf.Max(1, maxTileId + 1);
            var arr = new Color32[len];

            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] = new Color32(255, 255, 255, 255);
            }

            foreach (var kvp in _defs)
            {
                int tileId = kvp.Key;

                if ((uint)tileId >= (uint)arr.Length)
                    continue;

                TileDef def = kvp.Value;
                if (def == null)
                    continue;

                arr[tileId] = def.BaseColor;
            }

            return arr;
        }

        private float[] buildBaseColorInfluenceChannel(int maxTileId)
        {
            int len = Mathf.Max(1, maxTileId + 1);
            var arr = new float[len];

            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] = 1f;
            }

            foreach (var kvp in _defs)
            {
                int tileId = kvp.Key;

                if ((uint)tileId >= (uint)arr.Length)
                    continue;

                TileDef def = kvp.Value;
                if (def == null)
                    continue;

                arr[tileId] = def.BaseColorInfluence;
            }

            return arr;
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

        #region Base Color

        public bool TryGetBaseColor(int tileId, out Color32 baseColor)
        {
            if (tileId == _defaultTileId)
            {
                baseColor = new Color32(255, 255, 255, 255);
                return false;
            }

            if ((uint)tileId < (uint)_baseColors.Length)
            {
                baseColor = _baseColors[tileId];
                return true;
            }

            baseColor = new Color32(255, 255, 255, 255);
            return false;
        }

        public bool TryGetBaseColorInfluence(int tileId, out float influence)
        {
            if (tileId == _defaultTileId)
            {
                influence = 1f;
                return false;
            }

            if ((uint)tileId < (uint)_baseColorInfluences.Length)
            {
                influence = _baseColorInfluences[tileId];
                return true;
            }

            influence = 1f;
            return false;
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

        public bool TryGetColorBlend(int tileId, out float blend)
        {
            if (TryGetProperty<TileColorProperty>(tileId, out var colorProp) && colorProp != null)
            {
                blend = colorProp.Blend;
                return true;
            }

            blend = 0f;
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
