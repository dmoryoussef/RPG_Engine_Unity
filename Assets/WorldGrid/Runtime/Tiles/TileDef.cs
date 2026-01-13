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

        public TileDef(int tileId, string name, RectUv uv, IEnumerable<string> tags = null)
        {
            TileId = tileId;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Uv = uv;

            if (tags == null)
            {
                Tags = Array.Empty<string>();
            }
            else
            {
                // Normalize tags: trim, drop empties, distinct
                Tags = tags
                    .Select(t => t?.Trim())
                    .Where(t => !string.IsNullOrEmpty(t))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
        }

        public override string ToString()
        {
            if (Tags.Count == 0) return $"{Name} (id={TileId})";
            return $"{Name} (id={TileId}) [{string.Join(", ", Tags)}]";
        }
    }
}
