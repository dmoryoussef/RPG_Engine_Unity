// SparseChunkWorld.cs
using System;
using System.Collections.Generic;
using WorldGrid.Runtime.Chunks;
using WorldGrid.Runtime.Coords;
using WorldGrid.Runtime.Math;

namespace WorldGrid.Runtime.World
{
    /// <summary>
    /// Sparse, chunked tile world:
    /// - Chunks are created on first non-default write.
    /// - Chunks are removed when they return to all-default (to keep world sparse).
    /// - DirtyRenderChunks tracks chunk coords that need renderer rebuild.
    /// </summary>
    public sealed class SparseChunkWorld
    {
        public enum TileUpdateResult
        {
            NoChange = 0,
            ValueChanged = 1
        }

        public readonly struct TileUpdatedEvent
        {
            public readonly CellCoord Cell;
            public readonly ChunkCoord Chunk;

            public readonly int OldTileId;
            public readonly int NewTileId;

            public readonly TileUpdateResult Result;

            public TileUpdatedEvent(
                CellCoord cell,
                ChunkCoord chunk,
                int oldTileId,
                int newTileId,
                TileUpdateResult result)
            {
                Cell = cell;
                Chunk = chunk;
                OldTileId = oldTileId;
                NewTileId = newTileId;
                Result = result;
            }

            public override string ToString() =>
                $"Cell={Cell} Chunk={Chunk} {OldTileId}->{NewTileId} Result={Result}";
        }

        /// <summary>
        /// Raised when SetTile is called. Payload indicates whether the world state actually changed.
        /// </summary>
        public event Action<TileUpdatedEvent> TileUpdated;

        /// <summary>
        /// Raised when a previously-missing chunk is created due to a non-default write.
        /// </summary>
        public event Action<ChunkCoord> ChunkCreated;

        /// <summary>
        /// Raised when a chunk is removed (returned to all-default).
        /// </summary>
        public event Action<ChunkCoord> ChunkRemoved;

        /// <summary>
        /// Raised when a chunk changes storage representation (e.g., Uniform ↔ Dense).
        /// </summary>
        public event Action<ChunkCoord, ChunkStorageKind, ChunkStorageKind> ChunkStorageKindChanged;

        private readonly Dictionary<ChunkCoord, Chunk> _chunks;
        private readonly HashSet<ChunkCoord> _dirtyRenderChunks;

        public int ChunkSize { get; }
        public int DefaultTileId { get; }

        public int ChunkCount => _chunks.Count;

        /// <summary>
        /// Enumerates existing chunks (sparse). Useful for debug, profiling, and tooling.
        /// </summary>
        public IEnumerable<KeyValuePair<ChunkCoord, Chunk>> Chunks => _chunks;

        /// <summary>
        /// Chunks that need rendering rebuild. Renderer should clear these after rebuild.
        /// </summary>
        public IEnumerable<ChunkCoord> DirtyRenderChunks => _dirtyRenderChunks;

        public SparseChunkWorld(int chunkSize, int defaultTileId)
        {
            if (chunkSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(chunkSize), "chunkSize must be > 0");

            ChunkSize = chunkSize;
            DefaultTileId = defaultTileId;

            _chunks = new Dictionary<ChunkCoord, Chunk>();
            _dirtyRenderChunks = new HashSet<ChunkCoord>();
        }

        public bool TryGetChunk(ChunkCoord coord, out Chunk chunk) =>
            _chunks.TryGetValue(coord, out chunk);
        public bool HasChunk(ChunkCoord coord) =>
            _chunks.ContainsKey(coord);
        public int GetTile(int worldX, int worldY)
        {
            var world = new CellCoord(worldX, worldY);
            var cc = ChunkMath.WorldToChunk(world, ChunkSize);

            if (!_chunks.TryGetValue(cc, out var chunk))
                return DefaultTileId;

            var local = ChunkMath.WorldToLocal(world, ChunkSize);
            return chunk.Get(local.X, local.Y);
        }

        public void SetTile(int worldX, int worldY, int tileId)
        {
            var cell = new CellCoord(worldX, worldY);
            var cc = ChunkMath.WorldToChunk(cell, ChunkSize);
            var local = ChunkMath.WorldToLocal(cell, ChunkSize);

            // Missing chunk path
            if (!_chunks.TryGetValue(cc, out var chunk))
            {
                // Writing default into empty space is a no-op by design.
                if (tileId == DefaultTileId)
                {
                    TileUpdated?.Invoke(new TileUpdatedEvent(
                        cell, cc,
                        DefaultTileId, tileId,
                        TileUpdateResult.NoChange));
                    return;
                }

                // Create chunk on first non-default write.
                chunk = new Chunk(ChunkSize, DefaultTileId);
                _chunks.Add(cc, chunk);
                ChunkCreated?.Invoke(cc);

                // Old value is implicitly default in a missing chunk.
                int oldTileId = DefaultTileId;

                // No "prev kind" exists here (brand new chunk).
                chunk.Set(local.X, local.Y, tileId);
                MarkChunkDirtyIfNeeded(cc, chunk);

                TileUpdated?.Invoke(new TileUpdatedEvent(
                    cell, cc,
                    oldTileId, tileId,
                    TileUpdateResult.ValueChanged));

                return;
            }

            // Existing chunk path
            int oldValue = chunk.Get(local.X, local.Y);
            if (oldValue == tileId)
            {
                TileUpdated?.Invoke(new TileUpdatedEvent(
                    cell, cc,
                    oldValue, tileId,
                    TileUpdateResult.NoChange));
                return;
            }

            // Capture previous storage kind BEFORE the mutation.
            ChunkStorageKind prevKind = chunk.StorageKind;

            // Apply the mutation.
            chunk.Set(local.X, local.Y, tileId);
            MarkChunkDirtyIfNeeded(cc, chunk);

            // Check for storage kind transition AFTER the mutation (while chunk still exists).
            ChunkStorageKind newKind = chunk.StorageKind;
            if (newKind != prevKind)
                ChunkStorageKindChanged?.Invoke(cc, prevKind, newKind);

            // If we erased back to all-default, remove chunk (keeps world sparse).
            // IMPORTANT: keep this coord dirty so renderer rebuilds once with chunk missing,
            // clearing any existing mesh/overlay for that coord.
            if (tileId == DefaultTileId && ChunkIsAllDefault(chunk))
            {
                _chunks.Remove(cc);
                _dirtyRenderChunks.Add(cc);
                ChunkRemoved?.Invoke(cc);
            }

            TileUpdated?.Invoke(new TileUpdatedEvent(
                cell, cc,
                oldValue, tileId,
                TileUpdateResult.ValueChanged));
        }

        public void ClearDirtyRender(ChunkCoord coord)
        {
            if (_chunks.TryGetValue(coord, out var chunk))
                chunk.ClearDirtyRender();

            _dirtyRenderChunks.Remove(coord);
        }

        private void MarkChunkDirtyIfNeeded(ChunkCoord coord, Chunk chunk)
        {
            if (chunk.DirtyRender)
                _dirtyRenderChunks.Add(coord);
        }

        private bool ChunkIsAllDefault(Chunk chunk)
        {
            int size = chunk.Size;

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    if (chunk.Get(x, y) != DefaultTileId)
                        return false;

            return true;
        }

    }
}
