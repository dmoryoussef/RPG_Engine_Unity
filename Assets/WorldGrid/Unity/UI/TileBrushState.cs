using UnityEngine;

namespace WorldGrid.Unity.UI
{
    [CreateAssetMenu(
        menuName = "WorldGrid/UI/Tile Brush State",
        fileName = "TileBrushState")]
    public sealed class TileBrushState : ScriptableObject
    {
        [Tooltip("TileId to paint when LMB is held.")]
        public int selectedTileId = 1;

        [Tooltip("TileId to paint when RMB is held (erase). Usually 0.")]
        public int eraseTileId = 0;

        [Tooltip("0 = single cell. 1 = 3x3. 2 = 5x5, etc.")]
        public int brushRadius = 0;
    }
}
