using System;

namespace WorldPlacement.Runtime.Defs
{
    /// <summary>Pure C# definition for something that can be placed in the world.</summary>
    public sealed class PlacementDef
    {
        public string Id { get; }
        public string DisplayName { get; }
        public PlacementFootprint Footprint { get; }

        public PlacementDef(string id, string displayName, PlacementFootprint footprint)
        {
            Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("Id required.", nameof(id)) : id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? id : displayName;
            Footprint = footprint ?? throw new ArgumentNullException(nameof(footprint));
        }
    }
}
