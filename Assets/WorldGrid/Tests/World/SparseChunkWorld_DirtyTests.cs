using NUnit.Framework;
using WorldGrid.Runtime.Coords;
using WorldGrid.Runtime.World;

namespace WorldGrid.Tests.World
{
    public sealed class SparseChunkWorld_DirtyTests
    {
        private const int ChunkSize = 8;
        private const int DefaultTile = 0;

        [Test]
        public void DirtyChunk_IsReported_AfterWrite()
        {
            var w = new SparseChunkWorld(ChunkSize, DefaultTile);

            w.SetTile(0, 0, 1);

            CollectionAssert.Contains(
                w.DirtyRenderChunks,
                new ChunkCoord(0, 0)
            );
        }

        [Test]
        public void NoOpWrite_DoesNotMarkDirty()
        {
            var w = new SparseChunkWorld(ChunkSize, DefaultTile);

            w.SetTile(0, 0, 2);
            w.ClearDirtyRender(new ChunkCoord(0, 0));

            w.SetTile(0, 0, 2); // no-op

            Assert.That(w.DirtyRenderChunks, Is.Empty);
        }

        [Test]
        public void ClearingDirty_RemovesFromEnumeration()
        {
            var w = new SparseChunkWorld(ChunkSize, DefaultTile);

            w.SetTile(0, 0, 3);
            var cc = new ChunkCoord(0, 0);

            Assert.That(w.DirtyRenderChunks, Is.Not.Empty);

            w.ClearDirtyRender(cc);

            Assert.That(w.DirtyRenderChunks, Is.Empty);
        }

        [Test]
        public void RemovingChunk_RemovesDirtyState()
        {
            var w = new SparseChunkWorld(ChunkSize, DefaultTile);
            var cc = new ChunkCoord(0, 0);

            w.SetTile(0, 0, 4);
            Assert.That(w.DirtyRenderChunks, Is.Not.Empty);

            // Clear back to default → chunk removed
            w.SetTile(0, 0, DefaultTile);

            Assert.That(w.HasChunk(cc), Is.False);
            Assert.That(w.DirtyRenderChunks, Is.Empty);
        }
    }
}
