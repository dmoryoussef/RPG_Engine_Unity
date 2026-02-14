using System.IO;
using WorldGrid.Runtime.Chunks;
using WorldGrid.Runtime.Coords;

namespace WorldGrid.Runtime.Persistence
{
    /// <summary>
    /// Encodes/decodes ONE chunk to/from a binary stream.
    /// Used by both Save/Load and future streaming backends.
    /// </summary>
    public static class ChunkCodec
    {
        private const int ChunkFormatVersion = 1;

        private const byte FLAG_BASE_TILES = 1 << 0;
        private const byte FLAG_TILE_EXTRA = 1 << 1; // reserved for future per-tile blob

        public static void WriteChunk(
            BinaryWriter w,
            ChunkCoord coord,
            Chunk chunk,
            int defaultTileDefId)
        {
            w.Write(ChunkFormatVersion);

            w.Write(coord.X);
            w.Write(coord.Y);

            // For now: base tiles only, no extra blobs.
            byte flags = FLAG_BASE_TILES;
            w.Write(flags);

            int entryCount = CountNonDefaultTiles(chunk, defaultTileDefId);
            w.Write(entryCount);

            int size = chunk.Size;

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int defId = chunk.Get(x, y);
                    if (defId == defaultTileDefId)
                        continue;

                    ushort index = (ushort)(y * size + x);
                    w.Write(index);
                    w.Write(defId);

                    // Reserved for later:
                    // if ((flags & FLAG_TILE_EXTRA) != 0) { write extraLen + extraBytes }
                }
        }

        public static (ChunkCoord coord, Chunk chunk) ReadChunk(
            BinaryReader r,
            int chunkSize,
            int defaultTileDefId)
        {
            int version = r.ReadInt32();
            if (version != ChunkFormatVersion)
                throw new InvalidDataException($"Unsupported chunk format version {version}");

            int cx = r.ReadInt32();
            int cy = r.ReadInt32();
            var coord = new ChunkCoord(cx, cy);

            byte flags = r.ReadByte();
            int entryCount = r.ReadInt32();
            if (entryCount < 0) throw new InvalidDataException("Corrupt chunk: negative entryCount");

            var chunk = new Chunk(chunkSize, defaultTileDefId);

            for (int i = 0; i < entryCount; i++)
            {
                ushort index = r.ReadUInt16();
                int defId = r.ReadInt32();

                int x = index % chunkSize;
                int y = index / chunkSize;
                chunk.Set(x, y, defId);

                // If future saves include extras, older loaders can safely skip them:
                if ((flags & FLAG_TILE_EXTRA) != 0)
                {
                    ushort extraLen = r.ReadUInt16();
                    r.ReadBytes(extraLen); // ignore for now
                }
            }

            return (coord, chunk);
        }

        private static int CountNonDefaultTiles(Chunk chunk, int defaultTileDefId)
        {
            int size = chunk.Size;
            int count = 0;

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    if (chunk.Get(x, y) != defaultTileDefId)
                        count++;

            return count;
        }
    }
}
