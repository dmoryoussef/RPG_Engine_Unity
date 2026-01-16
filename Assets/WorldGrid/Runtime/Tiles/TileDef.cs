using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldGrid.Runtime.Tiles
{
    public sealed class TileDef
    {
        public int TileId { get; }
        public string Name { get; }
        public RectUv Uv { get; }

        /// <summary>
        /// Optional tags for debugging/querying. Keep as strings for now.
        /// </summary>
        public IReadOnlyList<string> Tags { get; }

        /// <summary>
        /// Optional extensible tile semantics (authoring/build-time),
        /// intended to be compiled into fast runtime channels in TileLibrary.
        /// </summary>
        public IReadOnlyList<TileProperty> Properties { get; }

        public TileDef(
            int tileId,
            string name,
            RectUv uv,
            IEnumerable<string> tags = null,
            IEnumerable<TileProperty> properties = null)
        {
            TileId = tileId;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Uv = uv;

            Tags = NormalizeTags(tags);
            Properties = NormalizeProperties(properties);
        }

        public bool TryGetProperty<T>(out T property)
            where T : TileProperty
        {
            for (int i = 0; i < Properties.Count; i++)
            {
                if (Properties[i] is T typed)
                {
                    property = typed;
                    return true;
                }
            }

            property = null;
            return false;
        }

        public override string ToString()
        {
            if (Tags.Count == 0) return $"{Name} (id={TileId})";
            return $"{Name} (id={TileId}) [{string.Join(", ", Tags)}]";
        }

        private static IReadOnlyList<string> NormalizeTags(IEnumerable<string> tags)
        {
            if (tags == null) return Array.Empty<string>();

            return tags
                .Select(t => t?.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static IReadOnlyList<TileProperty> NormalizeProperties(IEnumerable<TileProperty> properties)
        {
            if (properties == null) return Array.Empty<TileProperty>();

            // Enforce one property per concrete type.
            // If duplicates exist, the LAST one wins (authoring-friendly).
            Dictionary<Type, TileProperty> byType = null;

            foreach (var p in properties)
            {
                if (p == null) continue;

                byType ??= new Dictionary<Type, TileProperty>();
                byType[p.GetType()] = p;
            }

            if (byType == null || byType.Count == 0) return Array.Empty<TileProperty>();
            return byType.Values.ToArray();
        }
    }
}
