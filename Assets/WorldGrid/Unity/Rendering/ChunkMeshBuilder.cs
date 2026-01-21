using UnityEngine;
using WorldGrid.Runtime.Chunks;
using WorldGrid.Runtime.Coords;
using WorldGrid.Runtime.Tiles;

namespace WorldGrid.Unity.Rendering
{
    /// <summary>
    /// Builds the FOREGROUND tile mesh for a chunk (non-default tiles only).
    /// Mesh vertices are chunk-local. The chunk GameObject transform places it in world space.
    /// </summary>
    public static class ChunkMeshBuilder
    {
        public static void BuildChunkTilesMesh(
            MeshData md,
            Mesh mesh,
            ChunkCoord chunkCoord,      // used for deterministic color variation
            Chunk chunkOrNull,
            int chunkSize,
            int defaultTileId,
            TileLibrary tileLibrary,
            float cellSize
        )
        {
            md.Clear();

            if (tileLibrary == null)
            {
                // Upstream failure (provider not ready / exception). Build empty mesh to avoid spam.
                Apply(md, mesh);
                return;
            }

            // Missing chunk == uniform default -> no foreground quads.
            if (chunkOrNull == null)
            {
                Apply(md, mesh);
                return;
            }

            for (int ly = 0; ly < chunkSize; ly++)
                for (int lx = 0; lx < chunkSize; lx++)
                {
                    int tileId = chunkOrNull.Get(lx, ly);
                    if (tileId == defaultTileId)
                        continue;

                    if (!tileLibrary.TryGetUv(tileId, out var uv))
                        continue;

                    // Color channel (defaults are handled inside TileLibrary)
                    tileLibrary.TryGetColor(tileId, out var baseColor);
                    tileLibrary.TryGetColorJitter(tileId, out var jitter);

                    var finalColor = ApplyDeterministicJitter(baseColor, jitter, chunkCoord, lx, ly);

                    AddQuad_Local(md, cellSize, lx, ly, uv, finalColor);
                }

            Apply(md, mesh);
        }

        private static void Apply(MeshData md, Mesh mesh)
        {
            mesh.Clear();
            mesh.SetVertices(md.Vertices);
            mesh.SetUVs(0, md.Uvs);

            if (md.Colors.Count == md.Vertices.Count)
                mesh.SetColors(md.Colors);

            mesh.SetTriangles(md.Triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
        }

        private static void AddQuad_Local(MeshData md, float cellSize, int cellX, int cellY, RectUv uv, Color32 color)
        {
            Vector3 p0 = new Vector3(cellX * cellSize, cellY * cellSize, 0f);

            Vector3 v0 = p0;
            Vector3 v1 = p0 + new Vector3(cellSize, 0f, 0f);
            Vector3 v2 = p0 + new Vector3(cellSize, cellSize, 0f);
            Vector3 v3 = p0 + new Vector3(0f, cellSize, 0f);

            int i0 = md.Vertices.Count;
            md.Vertices.Add(v0);
            md.Vertices.Add(v1);
            md.Vertices.Add(v2);
            md.Vertices.Add(v3);

            md.Uvs.Add(new Vector2(uv.UMin, uv.VMin));
            md.Uvs.Add(new Vector2(uv.UMax, uv.VMin));
            md.Uvs.Add(new Vector2(uv.UMax, uv.VMax));
            md.Uvs.Add(new Vector2(uv.UMin, uv.VMax));

            // Vertex colors (tint/variation)
            md.Colors.Add(color);
            md.Colors.Add(color);
            md.Colors.Add(color);
            md.Colors.Add(color);

            md.Triangles.Add(i0 + 0);
            md.Triangles.Add(i0 + 2);
            md.Triangles.Add(i0 + 1);

            md.Triangles.Add(i0 + 0);
            md.Triangles.Add(i0 + 3);
            md.Triangles.Add(i0 + 2);
        }

        private static Color32 ApplyDeterministicJitter(Color32 c, float jitter, ChunkCoord chunkCoord, int lx, int ly)
        {
            if (jitter <= 0f)
                return c;

            uint h = Hash((uint)chunkCoord.X, (uint)chunkCoord.Y, (uint)lx, (uint)ly);

            // Map to [-1, +1]
            float t = ((h & 0xFFFFu) / 65535f) * 2f - 1f;
            float m = 1f + t * jitter;

            byte r = (byte)Mathf.Clamp(Mathf.RoundToInt(c.r * m), 0, 255);
            byte g = (byte)Mathf.Clamp(Mathf.RoundToInt(c.g * m), 0, 255);
            byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(c.b * m), 0, 255);
            return new Color32(r, g, b, c.a);
        }

        private static uint Hash(uint a, uint b, uint c, uint d)
        {
            // Simple deterministic mix (FNV-ish)
            uint x = 2166136261u;
            x = (x ^ a) * 16777619u;
            x = (x ^ b) * 16777619u;
            x = (x ^ c) * 16777619u;
            x = (x ^ d) * 16777619u;
            return x;
        }
    }
}
