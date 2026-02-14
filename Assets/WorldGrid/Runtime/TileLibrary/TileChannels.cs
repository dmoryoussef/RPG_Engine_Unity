namespace WorldGrid.Runtime.Tiles
{
    /// <summary>
    /// Compiled runtime channels derived from TileLibrary.
    /// Hardcoded for now; later generalized into a registry/builder system.
    /// </summary>
    public sealed class TileChannels
    {
        public GrassTileChannel Grass { get; }

        public TileChannels(TileLibrary lib, int maxTileIdToScan)
        {
            Grass = GrassTileChannel.BuildFrom(lib, maxTileIdToScan);
        }
    }
}
