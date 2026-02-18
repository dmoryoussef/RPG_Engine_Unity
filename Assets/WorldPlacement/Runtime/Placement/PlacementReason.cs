using WorldPlacement.Runtime.Grid;

namespace WorldPlacement.Runtime.Placement
{
    public readonly struct PlacementReason
    {
        public readonly string Message;
        public readonly Cell2i Cell;

        public PlacementReason(string message, Cell2i cell)
        {
            Message = message;
            Cell = cell;
        }

        public override string ToString() => $"{Message} @ {Cell}";
    }
}
