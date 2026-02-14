using UnityEngine;
using WorldGrid.Runtime.Chunks;
using WorldGrid.Runtime.Coords;
using WorldGrid.Runtime.Tiles;

namespace WorldGrid.Unity.Rendering
{
    /// <summary>
    /// Builds the foreground tile mesh for a chunk (non-default tiles only).
    /// Mesh vertices are chunk-local in XY. Z is baked per render layer; chunk transform places it in world space.
    /// </summary>
    public static class ChunkMeshBuilder
    {
        #region Public API

        public static void BuildChunkTilesMesh(
            MeshData md,
            Mesh mesh,
            ChunkCoord chunkCoord,
            Chunk chunkOrNull,
            int chunkSize,
            int defaultTileId,
            TileLibrary tileLibrary,
            float cellSize,
            float layerZ)
        {
            if (md == null || mesh == null)
                return;

            md.Clear();

            if (!canBuild(tileLibrary, chunkOrNull))
            {
                applyMesh(md, mesh);
                return;
            }

            buildTiles(md, chunkCoord, chunkOrNull, chunkSize, defaultTileId, tileLibrary, cellSize, layerZ);
            applyMesh(md, mesh);
        }

        #endregion

        #region Build

        private static bool canBuild(TileLibrary tileLibrary, Chunk chunkOrNull)
        {
            if (tileLibrary == null)
                return false;

            // Missing chunk means uniform default, so no foreground quads.
            return chunkOrNull != null;
        }

        private static void buildTiles(
            MeshData md,
            ChunkCoord chunkCoord,
            Chunk chunk,
            int chunkSize,
            int defaultTileId,
            TileLibrary tileLibrary,
            float cellSize,
            float layerZ)
        {
            for (int ly = 0; ly < chunkSize; ly++)
            {
                for (int lx = 0; lx < chunkSize; lx++)
                {
                    int tileId = chunk.Get(lx, ly);
                    if (tileId == defaultTileId)
                        continue;

                    if (!tileLibrary.TryGetUv(tileId, out var uv))
                        continue;

                    var color = computeTileColor(tileLibrary, tileId, chunkCoord, lx, ly);
                    addQuadLocal(md, cellSize, layerZ, lx, ly, uv, color);
                }
            }
        }

        private static Color32 computeTileColor(
            TileLibrary tileLibrary,
            int tileId,
            ChunkCoord chunkCoord,
            int lx,
            int ly)
        {
            // Tint is an overlay multiplier, blended from white -> tintColor by blend.
            // Jitter is an optional brightness variation (applied after blend).
            tileLibrary.TryGetColor(tileId, out var tintColor);
            tileLibrary.TryGetColorBlend(tileId, out var blend);
            tileLibrary.TryGetColorJitter(tileId, out var jitter);

            blend = Mathf.Clamp01(blend);
            jitter = Mathf.Max(0f, jitter);

            // White->Tint lerp so blend=0 means "no tint effect".
            Color final = Color.Lerp(Color.white, (Color)tintColor, blend);
            Color32 baked = (Color32)final;

            return applyDeterministicJitter(baked, jitter, chunkCoord, lx, ly);
        }

        #endregion

        #region Mesh Apply

        private static void applyMesh(MeshData md, Mesh mesh)
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

        #endregion

        #region Geometry

        private static void addQuadLocal(MeshData md, float cellSize, float layerZ, int cellX, int cellY, RectUv uv, Color32 color)
        {
            Vector3 p0 = new Vector3(cellX * cellSize, cellY * cellSize, layerZ);

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

        #endregion

        #region Jitter

        private static Color32 applyDeterministicJitter(Color32 c, float jitter, ChunkCoord chunkCoord, int lx, int ly)
        {
            if (jitter <= 0f)
                return c;

            uint h = hash((uint)chunkCoord.X, (uint)chunkCoord.Y, (uint)lx, (uint)ly);

            // Map to [-1, +1] using all 32 bits (reduces banding vs using low 16 bits only).
            float t = (h / 4294967295f) * 2f - 1f;

            float m = 1f + (t * jitter);

            byte r = (byte)Mathf.Clamp(Mathf.RoundToInt(c.r * m), 0, 255);
            byte g = (byte)Mathf.Clamp(Mathf.RoundToInt(c.g * m), 0, 255);
            byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(c.b * m), 0, 255);
            return new Color32(r, g, b, c.a);
        }

        // A small, fast integer mixer (better distribution than raw FNV for sequential coordinates).
        private static uint hash(uint a, uint b, uint c, uint d)
        {
            unchecked
            {
                uint x = 0x9E3779B9u; // golden ratio
                x ^= a + 0x85EBCA6Bu + (x << 6) + (x >> 2);
                x ^= b + 0xC2B2AE35u + (x << 6) + (x >> 2);
                x ^= c + 0x27D4EB2Fu + (x << 6) + (x >> 2);
                x ^= d + 0x165667B1u + (x << 6) + (x >> 2);

                // final avalanche (Murmur-like)
                x ^= x >> 16;
                x *= 0x7FEB352Du;
                x ^= x >> 15;
                x *= 0x846CA68Bu;
                x ^= x >> 16;
                return x;
            }
        }

        #endregion
    }
}
