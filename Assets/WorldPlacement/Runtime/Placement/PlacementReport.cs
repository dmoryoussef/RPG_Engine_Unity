using System.Collections.Generic;

namespace WorldPlacement.Runtime.Placement
{
    public sealed class PlacementReport
    {
        public PlacementStatus Status { get; private set; } = PlacementStatus.Allowed;
        public readonly List<PlacementReason> Reasons = new List<PlacementReason>(8);

        public bool Allowed => Status == PlacementStatus.Allowed;

        public void Clear()
        {
            Status = PlacementStatus.Allowed;
            Reasons.Clear();
        }

        public void Block(string message, Grid.Cell2i cell)
        {
            Status = PlacementStatus.Blocked;
            Reasons.Add(new PlacementReason(message, cell));
        }
    }
}
