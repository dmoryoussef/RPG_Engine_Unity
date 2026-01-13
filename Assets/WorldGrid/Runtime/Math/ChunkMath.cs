using System;
using WorldGrid.Runtime.Coords;

namespace WorldGrid.Runtime.Math
{
    public static class ChunkMath
    {
        public static ChunkCoord WorldToChunk(CellCoord world, int chunkSize)
        {
            if (chunkSize <= 0) throw new ArgumentOutOfRangeException(nameof(chunkSize), "chunkSize must be > 0");

            int cx = MathUtil.FloorDiv(world.X, chunkSize);
            int cy = MathUtil.FloorDiv(world.Y, chunkSize);
            return new ChunkCoord(cx, cy);
        }

        /// <summary>
        /// Local coordinate inside the chunk in [0..chunkSize-1].
        /// </summary>
        public static CellCoord WorldToLocal(CellCoord world, int chunkSize)
        {
            if (chunkSize <= 0) throw new ArgumentOutOfRangeException(nameof(chunkSize), "chunkSize must be > 0");

            int lx = MathUtil.FloorMod(world.X, chunkSize);
            int ly = MathUtil.FloorMod(world.Y, chunkSize);
            return new CellCoord(lx, ly);
        }

        public static int WorldToLocalX(int worldX, int chunkSize)
        {
            if (chunkSize <= 0) throw new ArgumentOutOfRangeException(nameof(chunkSize), "chunkSize must be > 0");
            return MathUtil.FloorMod(worldX, chunkSize);
        }

        public static int WorldToLocalY(int worldY, int chunkSize)
        {
            if (chunkSize <= 0) throw new ArgumentOutOfRangeException(nameof(chunkSize), "chunkSize must be > 0");
            return MathUtil.FloorMod(worldY, chunkSize);
        }
    }
}
