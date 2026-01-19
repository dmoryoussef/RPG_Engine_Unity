using UnityEngine;

namespace WorldGrid.Runtime.Tiles
{
    internal sealed class TileLibraryView : ITileLibraryView
    {
        public TileLibraryKey Key { get; }
        public TileLibrary Library { get; }
        public Texture2D AtlasTexture { get; }
        public Material AtlasMaterial { get; }

        public TileLibraryView(
            TileLibraryKey key,
            TileLibrary library,
            Texture2D atlasTexture,
            Material atlasMaterial)
        {
            Key = key;
            Library = library;
            AtlasTexture = atlasTexture;
            AtlasMaterial = atlasMaterial;
        }
    }
}
