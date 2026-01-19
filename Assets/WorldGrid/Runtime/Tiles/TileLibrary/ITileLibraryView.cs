using UnityEngine;

namespace WorldGrid.Runtime.Tiles
{
    /// <summary>
    /// A resolved, per-key "bundle" of tile data + presentation resources.
    /// Provider should return a stable instance per (world, key).
    /// </summary>
    public interface ITileLibraryView
    {
        TileLibraryKey Key { get; }

        /// <summary>
        /// Runtime lookup: tileId -> TileDef (uv, tags, properties).
        /// </summary>
        TileLibrary Library { get; }

        /// <summary>
        /// Atlas pixels for UI previews/tools. May be null for some backends.
        /// </summary>
        Texture2D AtlasTexture { get; }

        /// <summary>
        /// Material used for world rendering. Provider should supply an instance per (world, key).
        /// </summary>
        Material AtlasMaterial { get; }
    }
}
