using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorldGrid.Runtime.Tiles
{
    public sealed class TileDef
    {
        #region Properties

        public int TileId { get; }
        public string Name { get; }
        public RectUv Uv { get; }

        /// <summary>
        /// Intrinsic low-detail identity color for this tile.
        /// This is not tint; it is stable and authored/baked.
        /// </summary>
        public Color32 BaseColor { get; }

        /// <summary>
        /// Optional multiplier used by render systems that partially blend BaseColor.
        /// Defaults to 1.
        /// </summary>
        public float BaseColorInfluence { get; }

        public IReadOnlyList<string> Tags { get; }
        public IReadOnlyList<TileProperty> Properties { get; }

        #endregion

        #region Construction

        /// <summary>
        /// Backwards-compatible constructor (BaseColor defaults to white, influence defaults to 1).
        /// </summary>
        public TileDef(
            int tileId,
            string name,
            RectUv uv,
            IEnumerable<string> tags = null,
            IEnumerable<TileProperty> properties = null)
            : this(tileId, name, uv, new Color32(255, 255, 255, 255), 1f, tags, properties)
        {
        }

        public TileDef(
            int tileId,
            string name,
            RectUv uv,
            Color32 baseColor,
            float baseColorInfluence,
            IEnumerable<string> tags = null,
            IEnumerable<TileProperty> properties = null)
        {
            TileId = tileId;
            Name = name ?? string.Empty;
            Uv = uv;

            BaseColor = baseColor;
            BaseColorInfluence = Mathf.Clamp01(baseColorInfluence);

            Tags = normalizeTags(tags);
            Properties = normalizeProperties(properties);
        }

        #endregion

        #region Property Query

        public bool TryGetProperty<T>(out T property) where T : TileProperty
        {
            property = null;

            var props = Properties;
            if (props == null)
                return false;

            for (int i = 0; i < props.Count; i++)
            {
                if (props[i] is T match)
                {
                    property = match;
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Normalization

        private static IReadOnlyList<string> normalizeTags(IEnumerable<string> tags)
        {
            if (tags == null)
                return Array.Empty<string>();

            List<string> list = null;
            HashSet<string> seen = null;

            foreach (var raw in tags)
            {
                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                string tag = raw.Trim();

                if (seen == null)
                    seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (!seen.Add(tag))
                    continue;

                if (list == null)
                    list = new List<string>();

                list.Add(tag);
            }

            return list != null ? list : Array.Empty<string>();
        }

        private static IReadOnlyList<TileProperty> normalizeProperties(IEnumerable<TileProperty> properties)
        {
            if (properties == null)
                return Array.Empty<TileProperty>();

            List<TileProperty> list = null;

            foreach (var p in properties)
            {
                if (p == null)
                    continue;

                if (list == null)
                    list = new List<TileProperty>();

                list.Add(p);
            }

            return list != null ? list : Array.Empty<TileProperty>();
        }

        #endregion
    }
}
