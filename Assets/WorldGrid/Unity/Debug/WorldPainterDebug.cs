using UnityEngine;
using WorldGrid.Runtime.World;

namespace WorldGrid.Unity.Debug
{
    public sealed class WorldPainterDebug : MonoBehaviour
    {
        [SerializeField] private WorldHost worldHost;

        [Header("Paint Settings")]
        [SerializeField] private int originX = 0;
        [SerializeField] private int originY = 0;

        [Tooltip("Tile ids to paint in a row starting at origin.")]
        [SerializeField] private int[] paintTileIds = { 1, 2, 3, 4, 5 };

        private void Start()
        {
            if (worldHost == null)
            {
                UnityEngine.Debug.LogError("WorldPainterDebug: worldHost not assigned.", this);
                enabled = false;
                return;
            }

            SparseChunkWorld world = worldHost.World;

            for (int i = 0; i < paintTileIds.Length; i++)
                world.SetTile(originX + i, originY, paintTileIds[i]);

            // Also paint a small block
            for (int y = 1; y <= 3; y++)
                for (int x = 0; x <= 3; x++)
                    world.SetTile(originX + x, originY + y, paintTileIds[(x + y) % paintTileIds.Length]);
        }
    }
}
