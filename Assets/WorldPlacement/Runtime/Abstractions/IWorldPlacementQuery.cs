using WorldPlacement.Runtime.Grid;

namespace WorldPlacement.Runtime.Abstractions
{
    /// <summary>
    /// Runtime-only world query interface. Implemented by Unity/world adapters.
    /// MVP keeps it minimal.
    /// </summary>
    public interface IWorldPlacementQuery
    {
        /// <summary>Returns false if the cell is outside known world space.</summary>
        bool IsValidCell(Cell2i cell);

        /// <summary>MVP: can use tile id to filter placement (e.g., require non-default ground).</summary>
        bool TryGetTileId(Cell2i cell, out int tileId);
    }
}
