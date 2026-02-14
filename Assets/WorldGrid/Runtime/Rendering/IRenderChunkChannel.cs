using WorldGrid.Runtime.Tiles;
using WorldGrid.Runtime.World;
using WorldGrid.Unity; // <-- add (for WorldHost)

namespace WorldGrid.Runtime.Rendering
{
    public interface IRenderChunkChannel
    {
        void Bind(WorldHost host, SparseChunkWorld world, ITileLibraryView tiles, IChunkViewWindowProvider view);
        void Unbind();
        void Tick();
    }
}
