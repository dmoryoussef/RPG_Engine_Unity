// Stage 1 - HurtBoxRegistry.cs
// Purpose: Simple spatial hash that stores AABBs of all HurtBoxes for broadphase.
// Future me: cell size is a tuning knob; keep GC down by reusing lists if profiling warrants.

using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    public sealed class HurtBoxRegistry
    {
        public static bool HasInstance => _instance != null;
        public static HurtBoxRegistry Instance => _instance ??= new HurtBoxRegistry();
        static HurtBoxRegistry _instance;

        public float CellSize = 2.0f;

        // cell -> list of (HurtBox, Bounds)
        readonly Dictionary<Vector3Int, List<(HurtBox hb, Bounds aabb)>> _cells = new();
        // owner set -> cached entries so we can remove/update efficiently per frame
        readonly Dictionary<HurtBoxManager, List<(HurtBox hb, Bounds aabb, Vector3Int[] keys)>> _ownerCache = new();

        public void Register(HurtBoxManager set)
        {
            if (!_ownerCache.ContainsKey(set)) _ownerCache[set] = new();
        }

        public void Unregister(HurtBoxManager set)
        {
            if (!_ownerCache.TryGetValue(set, out var entries)) return;
            foreach (var (_, _, keys) in entries)
                foreach (var k in keys)
                    if (_cells.TryGetValue(k, out var list))
                        list.RemoveAll(e => e.hb == null || e.hb == null); // remove all entries owned by this set
            _ownerCache.Remove(set);
        }

        public void SyncAabbCache(HurtBoxManager set)
        {
            // Remove previous cells
            if (_ownerCache.TryGetValue(set, out var old))
            {
                foreach (var (hb, _, keys) in old)
                    foreach (var k in keys)
                        if (_cells.TryGetValue(k, out var list))
                            list.RemoveAll(e => e.hb == hb);
                old.Clear();
            }
            else _ownerCache[set] = new();

            // Re-add with latest bounds
            foreach (var hb in set.Boxes)
            {
                if (hb == null) continue;
                Bounds aabb = hb.Shape.Type == HurtShapeType.Sphere
                    ? new Bounds(hb.WorldCenter, Vector3.one * (hb.WorldRadius * 2f))
                    : new Bounds(hb.WorldCenter, hb.WorldHalfExtents * 2f);

                var keys = OverlappingKeys(aabb);
                foreach (var k in keys)
                {
                    if (!_cells.TryGetValue(k, out var list)) _cells[k] = list = new();
                    list.Add((hb, aabb));
                }

                _ownerCache[set].Add((hb, aabb, keys));
            }
        }

        public List<(HurtBox hb, Bounds aabb)> Query(Bounds queryAabb)
        {
            var results = new List<(HurtBox, Bounds)>(8);
            var seen = new HashSet<HurtBox>(); // avoids dupes when bounds spans multiple cells
            foreach (var key in OverlappingKeys(queryAabb))
            {
                if (!_cells.TryGetValue(key, out var list)) continue;
                foreach (var e in list)
                {
                    if (e.hb == null) continue;
                    if (!seen.Add(e.hb)) continue;
                    if (e.aabb.Intersects(queryAabb)) results.Add(e);
                }
            }
            return results;
        }

        Vector3Int[] OverlappingKeys(Bounds b)
        {
            Vector3Int ToKey(Vector3 v) => new(Mathf.FloorToInt(v.x / CellSize), Mathf.FloorToInt(v.y / CellSize), Mathf.FloorToInt(v.z / CellSize));
            var min = ToKey(b.min);
            var max = ToKey(b.max);
            var count = (max.x - min.x + 1) * (max.y - min.y + 1) * (max.z - min.z + 1);
            var arr = new Vector3Int[count];
            int i = 0;
            for (int x = min.x; x <= max.x; x++)
                for (int y = min.y; y <= max.y; y++)
                    for (int z = min.z; z <= max.z; z++)
                        arr[i++] = new Vector3Int(x, y, z);
            return arr;
        }
    }
}
