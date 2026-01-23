using System;
using System.Collections.Generic;

namespace WorldGrid.Runtime.Tiles
{
    public sealed class TileDef
    {
        #region Properties

        public int TileId { get; }
        public string Name { get; }
        public RectUv Uv { get; }
        public IReadOnlyList<string> Tags { get; }
        public IReadOnlyList<TileProperty> Properties { get; }

        #endregion

        #region Construction

        public TileDef(
            int tileId,
            string name,
            RectUv uv,
            IEnumerable<string> tags = null,
            IEnumerable<TileProperty> properties = null)
        {
            TileId = tileId;
            Name = name ?? string.Empty;
            Uv = uv;

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
                var t = raw?.Trim();
                if (string.IsNullOrEmpty(t))
                    continue;

                seen ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (!seen.Add(t))
                    continue;

                list ??= new List<string>();
                list.Add(t);
            }

            if (list == null || list.Count == 0)
                return Array.Empty<string>();

            return list.ToArray();
        }

        private static IReadOnlyList<TileProperty> normalizeProperties(IEnumerable<TileProperty> properties)
        {
            if (properties == null)
                return Array.Empty<TileProperty>();

            Dictionary<Type, TileProperty> map = null;

            foreach (var p in properties)
            {
                if (p == null)
                    continue;

                map ??= new Dictionary<Type, TileProperty>();
                map[p.GetType()] = p;
            }

            if (map == null || map.Count == 0)
                return Array.Empty<TileProperty>();

            var arr = new TileProperty[map.Count];
            int i = 0;
            foreach (var kvp in map)
                arr[i++] = kvp.Value;

            return arr;
        }

        #endregion
    }
}
