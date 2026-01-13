using System;
using UnityEngine;
using WorldGrid.Runtime.Coords;

namespace WorldGrid.Unity.Input
{
    /// <summary>
    /// A single pointer sample against the WorldGrid plane (z=0), resolved to cell and chunk coords.
    /// This packet is designed to be passed to debug tools or editor systems without redoing math.
    /// </summary>
    public readonly struct WorldPointerHit : IEquatable<WorldPointerHit>
    {
        public readonly bool Valid;

        public readonly Vector3 WorldPoint;
        public readonly Vector3 LocalPoint;

        public readonly CellCoord Cell;
        public readonly ChunkCoord Chunk;

        public readonly int LocalX;
        public readonly int LocalY;

        public readonly int TileId;

        public WorldPointerHit(
            bool valid,
            Vector3 worldPoint,
            Vector3 localPoint,
            CellCoord cell,
            ChunkCoord chunk,
            int localX,
            int localY,
            int tileId)
        {
            Valid = valid;

            WorldPoint = worldPoint;
            LocalPoint = localPoint;

            Cell = cell;
            Chunk = chunk;

            LocalX = localX;
            LocalY = localY;

            TileId = tileId;
        }

        public bool Equals(WorldPointerHit other)
        {
            // Hover-change comparisons should be stable and cheap.
            // Cell identity is the important part; include Valid to avoid false "same" when invalid.
            return Valid == other.Valid && Cell == other.Cell;
        }

        public override bool Equals(object obj) => obj is WorldPointerHit other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Valid, Cell);

        public static bool operator ==(WorldPointerHit a, WorldPointerHit b) => a.Equals(b);
        public static bool operator !=(WorldPointerHit a, WorldPointerHit b) => !a.Equals(b);

        public override string ToString()
        {
            if (!Valid)
                return "<WorldPointerHit Invalid>";

            return $"Cell={Cell} Chunk={Chunk} Local=({LocalX},{LocalY}) TileId={TileId}";
        }
    }
}
