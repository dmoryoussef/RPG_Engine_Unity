using UnityEngine;
using WorldGrid.Unity;                       // WorldHost
using WorldPlacement.Runtime.Grid;           // Cell2i

namespace WorldPlacement.Unity.Adapters
{
    /// <summary>
    /// MVP adapter that bridges WorldPlacement.Runtime to your existing SparseChunkWorld tile grid.
    /// Uses WorldHost.World.GetTile(x,y) to retrieve tile ids.
    ///
    /// Notes:
    /// - SparseChunkWorld is sparse, so "valid cell" is effectively always true.
    /// - If you later add world bounds, implement IsValidCell accordingly.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldGridTileQueryAdapter : WorldQueryAdapterBehaviour
    {
        [SerializeField] private WorldHost worldHost;

        private WorldGrid.Runtime.World.SparseChunkWorld _world;

        private void Start()
        {
            if (worldHost == null)
            {
                Debug.LogError("WorldGridTileQueryAdapter: worldHost not assigned.", this);
                enabled = false;
                return;
            }

            _world = worldHost.World;
            if (_world == null)
            {
                Debug.LogError("WorldGridTileQueryAdapter: worldHost.World is null (did WorldHost Awake run?).", this);
                enabled = false;
            }
        }

        public override bool IsValidCell(Cell2i cell)
        {
            // MVP: sparse world => any cell is "valid".
            // Later: return false if outside map bounds, forbidden region, unloaded chunk, etc.
            return _world != null;
        }

        public override bool TryGetTileId(Cell2i cell, out int tileId)
        {
            if (_world == null)
            {
                tileId = 0;
                return false;
            }

            tileId = _world.GetTile(cell.X, cell.Y);
            return true;
        }
    }
}
