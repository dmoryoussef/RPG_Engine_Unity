using System;

namespace WorldGrid.Runtime.Chunks
{
    public sealed class Chunk
    {
        private readonly int _size;
        private readonly int _area;

        private ChunkStorageKind _kind;

        // Uniform storage
        private int _uniformTileId;

        // Dense storage
        private int[] _dense; // length = _area when kind == Dense

        public int Size => _size;
        public ChunkStorageKind StorageKind => _kind;

        public bool DirtyRender { get; private set; }
        public bool DirtyCollision { get; private set; } // stub for now, but set on change

        public Chunk(int size, int uniformTileId)
        {
            if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size), "size must be > 0");

            _size = size;
            _area = checked(size * size);

            _kind = ChunkStorageKind.Uniform;
            _uniformTileId = uniformTileId;
            _dense = null;

            DirtyRender = false;
            DirtyCollision = false;
        }

        public int Get(int localX, int localY)
        {
            ValidateLocal(localX, localY);
            if (_kind == ChunkStorageKind.Uniform)
                return _uniformTileId;

            return _dense[ToIndex(localX, localY)];
        }

        /// <summary>
        /// Sets a tile at local coords. Marks DirtyRender/DirtyCollision only if value actually changed.
        /// </summary>
        public void Set(int localX, int localY, int tileId)
        {
            ValidateLocal(localX, localY);

            if (_kind == ChunkStorageKind.Uniform)
            {
                if (tileId == _uniformTileId)
                    return; // no change

                // Upgrade to dense and apply change
                UpgradeUniformToDense();
                int idx = ToIndex(localX, localY);

                if (_dense[idx] == tileId)
                    return; // (shouldn't happen, but keep invariant)

                _dense[idx] = tileId;
                MarkDirty();
                return;
            }

            // Dense
            int index = ToIndex(localX, localY);
            if (_dense[index] == tileId)
                return; // no change

            _dense[index] = tileId;
            MarkDirty();
        }

        public void ClearDirtyRender() => DirtyRender = false;
        public void ClearDirtyCollision() => DirtyCollision = false;

        private void MarkDirty()
        {
            DirtyRender = true;
            DirtyCollision = true; // stub for now
        }

        private void UpgradeUniformToDense()
        {
            if (_kind != ChunkStorageKind.Uniform)
                return;

            _dense = new int[_area];
            for (int i = 0; i < _area; i++)
                _dense[i] = _uniformTileId;

            _kind = ChunkStorageKind.Dense;
        }

        private int ToIndex(int localX, int localY) => localY * _size + localX;

        private void ValidateLocal(int localX, int localY)
        {
            if ((uint)localX >= (uint)_size) throw new ArgumentOutOfRangeException(nameof(localX), $"localX must be in [0..{_size - 1}]");
            if ((uint)localY >= (uint)_size) throw new ArgumentOutOfRangeException(nameof(localY), $"localY must be in [0..{_size - 1}]");
        }
    }
}
