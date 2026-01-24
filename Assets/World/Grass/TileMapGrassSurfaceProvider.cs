using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Grass
{
    public interface IGrassSurfaceProvider
    {
        void BuildPatchInstances(PatchId patch, List<GrassInstance> outInstances);
    }

    [DisallowMultipleComponent]
    public sealed class TilemapGrassSurfaceProvider : MonoBehaviour, IGrassSurfaceProvider
    {
        [Header("Tilemaps")]
        public Tilemap groundTilemap;
        [Tooltip("Optional: tiles here block grass entirely.")]
        public Tilemap blockingTilemap;

        [Header("Placement")]
        [Tooltip("How many tiles per patch (patch is patchSizeInTiles x patchSizeInTiles).")]
        public int patchSizeInTiles = 8;

        [Tooltip("Grass spawns within this normalized padding inside a tile. 0.1 = keep away from edges.")]
        [Range(0f, 0.45f)]
        public float tilePadding = 0.12f;

        [Header("Density")]
        [Tooltip("Average clumps per tile. Fractional values are supported stochastically.")]
        [Range(0f, 8f)]
        public float clumpsPerTile = 1.5f;

        [Tooltip("Safety cap to prevent runaway generation.")]
        public int maxClumpsPerPatch = 4000;

        [Header("Variation")]
        [Range(0.7f, 1.3f)]
        public float scaleMin = 0.9f;
        [Range(0.7f, 1.6f)]
        public float scaleMax = 1.15f;

        [Tooltip("Base tint range. Final tint gets additional subtle noise in shader.")]
        public Color tintA = new Color(0.25f, 0.55f, 0.25f, 1f);
        public Color tintB = new Color(0.18f, 0.45f, 0.18f, 1f);

        [Header("Determinism")]
        public int globalSeed = 12345;

        public void BuildPatchInstances(PatchId patch, List<GrassInstance> outInstances)
        {
            outInstances.Clear();

            if (groundTilemap == null)
            {
                Debug.LogError("TilemapGrassSurfaceProvider: groundTilemap is null.");
                return;
            }

            int ps = Mathf.Max(1, patchSizeInTiles);

            int x0 = patch.x * ps;
            int y0 = patch.y * ps;

            // NOTE: we treat patch coords as cell coords / ps.
            // We iterate cell positions [x0..x0+ps-1], [y0..y0+ps-1]
            for (int cy = y0; cy < y0 + ps; cy++)
            {
                for (int cx = x0; cx < x0 + ps; cx++)
                {
                    var cell = new Vector3Int(cx, cy, 0);

                    if (!groundTilemap.HasTile(cell))
                        continue;

                    if (blockingTilemap != null && blockingTilemap.HasTile(cell))
                        continue;

                    // Decide how many clumps in this tile:
                    int baseCount = Mathf.FloorToInt(clumpsPerTile);
                    float frac = Mathf.Clamp01(clumpsPerTile - baseCount);

                    // Deterministic RNG per cell:
                    uint cellSeed = HashToUint(globalSeed, cx, cy);
                    var rng = new XorShift32(cellSeed);

                    int count = baseCount + (rng.NextFloat01() < frac ? 1 : 0);
                    if (count <= 0) continue;

                    // World-space tile origin
                    Vector3 tileWorld = groundTilemap.GetCellCenterWorld(cell);
                    Vector3 cellSize = groundTilemap.cellSize;

                    // Spawn jitter inside tile bounds
                    for (int i = 0; i < count; i++)
                    {
                        if (outInstances.Count >= maxClumpsPerPatch) return;

                        float jx = rng.Range(-0.5f + tilePadding, 0.5f - tilePadding);
                        float jy = rng.Range(-0.5f + tilePadding, 0.5f - tilePadding);

                        Vector3 pos = tileWorld + new Vector3(jx * cellSize.x, jy * cellSize.y, 0f);

                        float rot = rng.Range(0f, Mathf.PI * 2f);
                        float scale = rng.Range(scaleMin, scaleMax);

                        // Tint pick between A/B + tiny random bias
                        float t = rng.NextFloat01();
                        Color tint = Color.Lerp(tintA, tintB, t);
                        float bias = rng.Range(0.92f, 1.08f);
                        tint.r *= bias; tint.g *= bias; tint.b *= bias;

                        outInstances.Add(new GrassInstance
                        {
                            position = pos,
                            rotationRad = rot,
                            scale = scale,
                            tint = tint
                        });
                    }
                }
            }
        }

        // ---------- Deterministic RNG helpers ----------
        private static uint HashToUint(int seed, int x, int y)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)seed) * 16777619u;
                h = (h ^ (uint)x) * 16777619u;
                h = (h ^ (uint)y) * 16777619u;
                h ^= h >> 13;
                h *= 1274126177u;
                h ^= h >> 16;
                return h;
            }
        }

        private struct XorShift32
        {
            private uint _state;
            public XorShift32(uint seed) { _state = seed == 0 ? 0x6C8E9CF5u : seed; }

            public uint NextU()
            {
                uint x = _state;
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
                _state = x;
                return x;
            }

            public float NextFloat01() => (NextU() & 0x00FFFFFFu) / 16777216f;

            public float Range(float a, float b) => a + (b - a) * NextFloat01();
        }
    }
}
