using NUnit.Framework;
using WorldGrid.Runtime.Coords;
using WorldGrid.Runtime.Math;

namespace WorldGrid.Tests.Coords
{
    public sealed class ChunkMathTests
    {
        private const int ChunkSize = 32;

        [Test]
        public void WorldToChunk_And_Local_PositiveEdges()
        {
            var world = new CellCoord(31, 0);
            Assert.That(ChunkMath.WorldToChunk(world, ChunkSize), Is.EqualTo(new ChunkCoord(0, 0)));
            Assert.That(ChunkMath.WorldToLocal(world, ChunkSize), Is.EqualTo(new CellCoord(31, 0)));

            world = new CellCoord(32, 0);
            Assert.That(ChunkMath.WorldToChunk(world, ChunkSize), Is.EqualTo(new ChunkCoord(1, 0)));
            Assert.That(ChunkMath.WorldToLocal(world, ChunkSize), Is.EqualTo(new CellCoord(0, 0)));
        }

        [Test]
        public void WorldToChunk_And_Local_NegativeEdges()
        {
            var world = new CellCoord(-1, 0);
            Assert.That(ChunkMath.WorldToChunk(world, ChunkSize), Is.EqualTo(new ChunkCoord(-1, 0)));
            Assert.That(ChunkMath.WorldToLocal(world, ChunkSize), Is.EqualTo(new CellCoord(31, 0)));

            world = new CellCoord(-33, 0);
            Assert.That(ChunkMath.WorldToChunk(world, ChunkSize), Is.EqualTo(new ChunkCoord(-2, 0)));
            Assert.That(ChunkMath.WorldToLocal(world, ChunkSize), Is.EqualTo(new CellCoord(31, 0)));
        }

        [Test]
        public void WorldToLocal_IsAlwaysInRange()
        {
            // A small sampling including negative values
            for (int x = -100; x <= 100; x += 7)
                for (int y = -100; y <= 100; y += 11)
                {
                    var local = ChunkMath.WorldToLocal(new CellCoord(x, y), ChunkSize);
                    Assert.That(local.X, Is.InRange(0, ChunkSize - 1));
                    Assert.That(local.Y, Is.InRange(0, ChunkSize - 1));
                }
        }
    }
}
