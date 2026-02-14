namespace WorldGrid.Runtime.Tiles
{
    /// <summary>
    /// Stable consumer-facing access point for tile libraries.
    /// Consumers must not retain independent references to assets/atlases/materials.
    /// </summary>
    public interface ITileLibrarySource
    {
        bool Has(TileLibraryKey key);

        /// <summary>
        /// Get the resolved library view for a given key.
        /// Recommended: call Has(key) first if missing keys are possible in your context.
        /// </summary>
        ITileLibraryView Get(TileLibraryKey key);
    }
}
