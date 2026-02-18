using System;
using System.Collections.Generic;
using WorldPlacement.Runtime.Abstractions;
using WorldPlacement.Runtime.Defs;
using WorldPlacement.Runtime.Grid;
using WorldPlacement.Runtime.Placement;

namespace WorldPlacement.Runtime.Systems
{
    /// <summary>
    /// MVP: registry + occupancy lookup + placement evaluation.
    /// No Unity refs. No tilemap dependency (uses IWorldPlacementQuery).
    /// </summary>
    public sealed class WorldPlacementSystem
    {
        public event Action<PlacedInstance> Placed;
        public event Action<int> Removed;
        public event Action<PlacementDef, Cell2i, Rotation4, PlacementReport> PlacementBlocked;

        private readonly IWorldPlacementQuery _world;
        private readonly int _defaultTileId;

        private readonly Dictionary<int, PlacedInstance> _instances = new Dictionary<int, PlacedInstance>(128);
        private readonly Dictionary<Cell2i, int> _occupancy = new Dictionary<Cell2i, int>(1024);

        private int _nextId = 1;
        private readonly List<Cell2i> _scratchFootprint = new List<Cell2i>(64);

        public IReadOnlyDictionary<int, PlacedInstance> Instances => _instances;

        public WorldPlacementSystem(IWorldPlacementQuery worldQuery, int defaultTileId)
        {
            _world = worldQuery ?? throw new ArgumentNullException(nameof(worldQuery));
            _defaultTileId = defaultTileId;
        }

        public bool TryGetAt(Cell2i cell, out PlacedInstance instance)
        {
            instance = null;
            if (_occupancy.TryGetValue(cell, out int id))
                return _instances.TryGetValue(id, out instance);

            return false;
        }

        public bool IsOccupied(Cell2i cell) => _occupancy.ContainsKey(cell);

        public PlacementReport Evaluate(PlacementDef def, Cell2i anchor, Rotation4 rotation, bool requireNonDefaultTile)
        {
            var report = new PlacementReport();
            report.Clear();

            if (def == null)
            {
                report.Block("No placement definition.", anchor);
                return report;
            }

            def.Footprint.GetOccupiedWorldCells(anchor, rotation, _scratchFootprint);

            // Validity checks
            foreach (var c in _scratchFootprint)
            {
                if (!_world.IsValidCell(c))
                {
                    report.Block("Out of bounds / invalid cell.", c);
                    return report;
                }
            }

            // Terrain filter (MVP)
            if (requireNonDefaultTile)
            {
                foreach (var c in _scratchFootprint)
                {
                    if (!_world.TryGetTileId(c, out int tileId))
                    {
                        report.Block("No terrain data for cell.", c);
                        return report;
                    }
                    if (tileId == _defaultTileId)
                    {
                        report.Block("Requires painted ground (non-default tile).", c);
                        return report;
                    }
                }
            }

            // Occupancy collision
            foreach (var c in _scratchFootprint)
            {
                if (_occupancy.ContainsKey(c))
                {
                    report.Block("Cell already occupied.", c);
                    return report;
                }
            }

            return report; // Allowed
        }

        public bool TryPlace(PlacementDef def, Cell2i anchor, Rotation4 rotation, bool requireNonDefaultTile, out PlacedInstance instance)
        {
            instance = null;

            var report = Evaluate(def, anchor, rotation, requireNonDefaultTile);
            if (!report.Allowed)
            {
                PlacementBlocked?.Invoke(def, anchor, rotation, report);
                return false;
            }

            def.Footprint.GetOccupiedWorldCells(anchor, rotation, _scratchFootprint);

            var footprintCopy = new List<Cell2i>(_scratchFootprint.Count);
            for (int i = 0; i < _scratchFootprint.Count; i++)
                footprintCopy.Add(_scratchFootprint[i]);

            int id = _nextId++;
            instance = new PlacedInstance(id, def.Id, anchor, rotation, footprintCopy);
            _instances.Add(id, instance);

            for (int i = 0; i < footprintCopy.Count; i++)
                _occupancy[footprintCopy[i]] = id;

            Placed?.Invoke(instance);
            return true;
        }

        public bool RemoveInstance(int instanceId)
        {
            if (!_instances.TryGetValue(instanceId, out var inst))
                return false;

            var cells = inst.FootprintWorldCells;
            for (int i = 0; i < cells.Count; i++)
            {
                var c = cells[i];
                if (_occupancy.TryGetValue(c, out int occupant) && occupant == instanceId)
                    _occupancy.Remove(c);
            }

            _instances.Remove(instanceId);
            Removed?.Invoke(instanceId);
            return true;
        }
    }
}
