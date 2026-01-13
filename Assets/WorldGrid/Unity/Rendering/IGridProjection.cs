using UnityEngine;

namespace WorldGrid.Unity.Rendering
{
    public interface IGridProjection
    {
        float CellSize { get; }
        Vector3 CellToWorld(int worldX, int worldY);
    }
}
