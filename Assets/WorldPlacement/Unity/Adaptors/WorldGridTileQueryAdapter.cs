using UnityEngine;
using WorldGrid.Unity;                       // WorldHost
using WorldPlacement.Runtime.Grid;           // Cell2i

namespace WorldPlacement.Unity.Adapters
{
    [DisallowMultipleComponent]
    public sealed class WorldGridTileQueryAdapter : WorldQueryAdapterBehaviour
    {
        [SerializeField] private WorldHost worldHost;

        private WorldGrid.Runtime.World.SparseChunkWorld _world;
        private bool _warned;

        private void Start()
        {
            if (worldHost == null)
            {
                WarnOnce("WorldGridTileQueryAdapter: worldHost not assigned.");
                enabled = false;
                return;
            }

            _world = worldHost.World;
            if (_world == null)
            {
                WarnOnce("WorldGridTileQueryAdapter: worldHost.World is null (init order issue?).");
                enabled = false;
            }
        }

        public override bool IsValidCell(Cell2i cell)
        {
            // MVP: sparse world => treat all cells as valid if the world exists.
            return _world != null;
        }

        public override bool TryGetTileId(Cell2i cell, out int tileId)
        {
            if (_world == null)
            {
                WarnOnce("WorldGridTileQueryAdapter: world is null; cannot query tile ids.");
                tileId = 0;
                return false;
            }

            tileId = _world.GetTile(cell.X, cell.Y);
            return true;
        }

        private void WarnOnce(string msg)
        {
            if (_warned) return;
            _warned = true;
            UnityEngine.Debug.LogWarning(msg, this);
        }
    }
}
