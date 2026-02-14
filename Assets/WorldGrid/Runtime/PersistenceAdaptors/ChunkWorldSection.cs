
using Persistence;
using System.IO;
using WorldGrid.Runtime.Persistence;
using WorldGrid.Runtime.World;

namespace WorldGrid.Runtime.SaveAdapters
{
    /// <summary>
    /// Saves/loads the SparseChunkWorld as one record inside the generic save container.
    /// </summary>
    public sealed class ChunkWorldSection : IDataSection
    {
        public string Key => "worldgrid/sparsechunkworld";
        public int Version => 1;

        public void Write(SaveContext ctx, BinaryWriter w)
        {
            var world = ctx.Get<SparseChunkWorld>();

            w.Write(world.ChunkSize);
            w.Write(world.DefaultTileId);

            w.Write(world.ChunkCount);

            foreach (var kvp in world.Chunks)
            {
                ChunkCodec.WriteChunk(w, kvp.Key, kvp.Value, world.DefaultTileId);
            }
        }

        public void Read(SaveContext ctx, BinaryReader r, int version)
        {
            int chunkSize = r.ReadInt32();
            int defaultTileId = r.ReadInt32();

            int chunkCount = r.ReadInt32();
            if (chunkCount < 0) throw new InvalidDataException("Corrupt save: negative chunkCount");

            // Reuse existing world if present and compatible, otherwise create.
            if (!ctx.TryGet<SparseChunkWorld>(out var world) ||
                world.ChunkSize != chunkSize ||
                world.DefaultTileId != defaultTileId)
            {
                world = new SparseChunkWorld(chunkSize, defaultTileId);
                ctx.Set(world);
            }

            world.ClearAllChunksForLoad();

            for (int i = 0; i < chunkCount; i++)
            {
                var (coord, chunk) = ChunkCodec.ReadChunk(r, chunkSize, defaultTileId);
                world.AddChunkForLoad(coord, chunk);
            }
        }

    }
}
