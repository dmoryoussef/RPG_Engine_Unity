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
            ChunkCoord chunkCoord,      // kept for symmetry/debugging; not used for vertex placement
            Chunk chunkOrNull,
            int chunkSize,
            int defaultTileId,
            TileLibrary tileLibrary,
            float cellSize
        )
        {
            md.Clear();

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

                    AddQuad_Local(md, cellSize, lx, ly, uv);
                }

            Apply(md, mesh);
        }

        private static void Apply(MeshData md, Mesh mesh)
        {
            mesh.Clear();
            mesh.SetVertices(md.Vertices);
            mesh.SetUVs(0, md.Uvs);
            mesh.SetTriangles(md.Triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
        }

        private static void AddQuad_Local(MeshData md, float cellSize, int cellX, int cellY, RectUv uv)
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

            md.Triangles.Add(i0 + 0);
            md.Triangles.Add(i0 + 2);
            md.Triangles.Add(i0 + 1);

            md.Triangles.Add(i0 + 0);
            md.Triangles.Add(i0 + 3);
            md.Triangles.Add(i0 + 2);
        }
    }
}
