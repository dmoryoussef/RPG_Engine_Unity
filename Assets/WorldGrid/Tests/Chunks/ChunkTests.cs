using NUnit.Framework;
using WorldGrid.Runtime.Chunks;

namespace WorldGrid.Tests.Chunks
{
    public sealed class ChunkTests
    {
        private const int Size = 8;

        [Test]
        public void Uniform_Get_ReturnsUniformForAllCells()
        {
            var c = new Chunk(Size, uniformTileId: 5);

            Assert.That(c.StorageKind, Is.EqualTo(ChunkStorageKind.Uniform));

            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    Assert.That(c.Get(x, y), Is.EqualTo(5));
        }

        [Test]
        public void Uniform_SetSameValue_IsNoOp_AndDoesNotDirty()
        {
            var c = new Chunk(Size, uniformTileId: 5);

            c.Set(3, 4, 5);

            Assert.That(c.StorageKind, Is.EqualTo(ChunkStorageKind.Uniform));
            Assert.That(c.DirtyRender, Is.False);
            Assert.That(c.DirtyCollision, Is.False);
        }

        [Test]
        public void Uniform_SetDifferentValue_UpgradesToDense_AndMarksDirty()
        {
            var c = new Chunk(Size, uniformTileId: 5);

            c.Set(3, 4, 7);

            Assert.That(c.StorageKind, Is.EqualTo(ChunkStorageKind.Dense));
            Assert.That(c.DirtyRender, Is.True);
            Assert.That(c.DirtyCollision, Is.True);

            // changed cell
            Assert.That(c.Get(3, 4), Is.EqualTo(7));

            // unchanged cells retain prior uniform value
            Assert.That(c.Get(0, 0), Is.EqualTo(5));
            Assert.That(c.Get(Size - 1, Size - 1), Is.EqualTo(5));
        }

        [Test]
        public void Dense_SetSameValue_IsNoOp()
        {
            var c = new Chunk(Size, uniformTileId: 1);

            // force dense
            c.Set(0, 0, 2);
            Assert.That(c.StorageKind, Is.EqualTo(ChunkStorageKind.Dense));

            c.ClearDirtyRender();
            c.ClearDirtyCollision();

            // same value again
            c.Set(0, 0, 2);

            Assert.That(c.DirtyRender, Is.False);
            Assert.That(c.DirtyCollision, Is.False);
        }

        [Test]
        public void Dense_SetDifferentValues_ReadsBackCorrectly()
        {
            var c = new Chunk(Size, uniformTileId: 0);

            // force dense by changing one cell
            c.Set(0, 0, 9);
            Assert.That(c.StorageKind, Is.EqualTo(ChunkStorageKind.Dense));

            c.Set(7, 7, 4);
            c.Set(1, 6, 3);

            Assert.That(c.Get(0, 0), Is.EqualTo(9));
            Assert.That(c.Get(7, 7), Is.EqualTo(4));
            Assert.That(c.Get(1, 6), Is.EqualTo(3));

            // cells not explicitly changed should still be uniform-origin value (0)
            Assert.That(c.Get(2, 2), Is.EqualTo(0));
        }

        [Test]
        public void Get_OutOfRange_Throws()
        {
            var c = new Chunk(Size, uniformTileId: 0);

            Assert.That(() => c.Get(-1, 0), Throws.InstanceOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => c.Get(0, -1), Throws.InstanceOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => c.Get(Size, 0), Throws.InstanceOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => c.Get(0, Size), Throws.InstanceOf<System.ArgumentOutOfRangeException>());
        }

        [Test]
        public void Set_OutOfRange_Throws()
        {
            var c = new Chunk(Size, uniformTileId: 0);

            Assert.That(() => c.Set(-1, 0, 1), Throws.InstanceOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => c.Set(0, -1, 1), Throws.InstanceOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => c.Set(Size, 0, 1), Throws.InstanceOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => c.Set(0, Size, 1), Throws.InstanceOf<System.ArgumentOutOfRangeException>());
        }
    }
}
