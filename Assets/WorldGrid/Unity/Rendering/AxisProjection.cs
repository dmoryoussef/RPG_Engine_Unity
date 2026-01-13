using UnityEngine;

namespace WorldGrid.Unity.Rendering
{
    public sealed class AxisProjection : IGridProjection
    {
        public float CellSize { get; }

        public AxisProjection(float cellSize)
        {
            CellSize = cellSize;
        }

        public Vector3 CellToWorld(int worldX, int worldY)
        {
            return new Vector3(worldX * CellSize, worldY * CellSize, 0f);
        }
    }
}
