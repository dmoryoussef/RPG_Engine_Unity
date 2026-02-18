using UnityEngine;
using WorldPlacement.Runtime.Abstractions;
using WorldPlacement.Runtime.Grid;

namespace WorldPlacement.Unity.Adapters
{
    /// <summary>
    /// Unity-side adapter hook. implement the actual bridge to world/map here.
    /// This stays in Unity so Runtime remains pure.
    ///
    /// MVP: can keep it dumb and just call into existing map system.
    /// </summary>
    public abstract class WorldQueryAdapterBehaviour : MonoBehaviour, IWorldPlacementQuery
    {
        public abstract bool IsValidCell(Cell2i cell);
        public abstract bool TryGetTileId(Cell2i cell, out int tileId);
    }
}
