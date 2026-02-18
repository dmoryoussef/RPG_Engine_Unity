using System.Collections.Generic;
using WorldPlacement.Runtime.Grid;

namespace WorldPlacement.Runtime.Systems
{
    public sealed class PlacedInstance
    {
        public int InstanceId { get; }
        public string DefId { get; }
        public Cell2i Anchor { get; }
        public Rotation4 Rotation { get; }

        /// <summary>Cached world footprint for fast removal.</summary>
        public readonly List<Cell2i> FootprintWorldCells;

        public PlacedInstance(int instanceId, string defId, Cell2i anchor, Rotation4 rotation, List<Cell2i> footprintWorldCells)
        {
            InstanceId = instanceId;
            DefId = defId;
            Anchor = anchor;
            Rotation = rotation;
            FootprintWorldCells = footprintWorldCells;
        }
    }
}
