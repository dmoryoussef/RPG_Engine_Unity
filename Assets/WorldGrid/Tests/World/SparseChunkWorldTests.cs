using NUnit.Framework;
using WorldGrid.Runtime.Coords;
using WorldGrid.Runtime.World;

namespace WorldGrid.Tests.World
{
    public sealed class SparseChunkWorldTests
    {
        private const int ChunkSize = 8;
        private const int DefaultTile = 0;

        [Test]
        public void GetTile_FromMissingChunk_ReturnsDefault()
        {
            var w = new SparseChunkWorld(ChunkSize, DefaultTile);

            Assert.That(w.GetTile(0, 0), Is.EqualTo(DefaultTile));
            Assert.That(w.GetTile(999, -123), Is.EqualTo(DefaultTile));
        }

        [Test]
        public void SetTile_DefaultIntoMissingChunk_DoesNotCreateChunk()
        {
            var w = new SparseChunkWorld(ChunkSize, DefaultTile);

            w.SetTile(0, 0, DefaultTile);
            w.SetTile(-1, 5, DefaultTile);

            Assert.That(w.ChunkCount, Is.EqualTo(0));
        }

        [Test]
        public void SetTile_NonDefault_CreatesChunk_AndReadsBack()
        {
            var w = new SparseChunkWorld(ChunkSize, DefaultTile);

            w.SetTile(0, 0, 7);

            Assert.That(w.ChunkCount, Is.EqualTo(1));
            Assert.That(w.GetTile(0, 0), Is.EqualTo(7));
        }

        [Test]
        public void NegativeCoordinates_Work_AndCreateCorrectChunk()
        {
            var w = new SparseChunkWorld(ChunkSize, DefaultTile);

            w.SetTile(-1, 0, 9);
            Assert.That(w.GetTile(-1, 0), Is.EqualTo(9));

            // (-1,0) should be in chunk (-1,0) for any chunk size > 0
            Assert.That(w.HasChunk(new ChunkCoord(-1, 0)), Is.True);
        }

        [Test]
        public void SetTile_NoOp_WhenValueAlreadySame()
        {
            var w = new SparseChunkWorld(ChunkSize, DefaultTile);

            w.SetTile(0, 0, 3);
            int chunksAfterFirst = w.ChunkCount;

            w.SetTile(0, 0, 3); // no-op
            Assert.That(w.ChunkCount, Is.EqualTo(chunksAfterFirst));
            Assert.That(w.GetTile(0, 0), Is.EqualTo(3));
        }

        [Test]
        public void ClearingLastNonDefaultTile_RemovesChunk()
        {
            var w = new SparseChunkWorld(ChunkSize, DefaultTile);

            w.SetTile(0, 0, 1);
            Assert.That(w.ChunkCount, Is.EqualTo(1));

            // Clear it back to default; chunk should become empty and be removed.
            w.SetTile(0, 0, DefaultTile);

            Assert.That(w.GetTile(0, 0), Is.EqualTo(DefaultTile));
            Assert.That(w.ChunkCount, Is.EqualTo(0));
        }

        [Test]
        public void ChunkNotRemoved_IfOtherNonDefaultTilesRemain()
        {
            var w = new SparseChunkWorld(ChunkSize, DefaultTile);

            w.SetTile(0, 0, 1);
            w.SetTile(1, 0, 2);

            Assert.That(w.ChunkCount, Is.EqualTo(1));

            // Clear one tile, but other remains
            w.SetTile(0, 0, DefaultTile);

            Assert.That(w.ChunkCount, Is.EqualTo(1));
            Assert.That(w.GetTile(0, 0), Is.EqualTo(DefaultTile));
            Assert.That(w.GetTile(1, 0), Is.EqualTo(2));
        }

        [Test]
        public void TilesAcrossChunkBoundary_CreateTwoChunks()
        {
            var w = new SparseChunkWorld(ChunkSize, DefaultTile);

            // (ChunkSize-1,0) and (ChunkSize,0) are in adjacent chunks
            w.SetTile(ChunkSize - 1, 0, 5);
            w.SetTile(ChunkSize, 0, 6);

            Assert.That(w.GetTile(ChunkSize - 1, 0), Is.EqualTo(5));
            Assert.That(w.GetTile(ChunkSize, 0), Is.EqualTo(6));
            Assert.That(w.ChunkCount, Is.EqualTo(2));
        }
    }
}
