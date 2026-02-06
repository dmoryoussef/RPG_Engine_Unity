#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Grass.Editor
{
    public static class GrassClumpMeshGenerator3D
    {
        public static void GenerateIntoProfile(Grass.GrassRenderProfile profile)
        {
            const float width = 0.35f;
            const float height = 1.0f;
            int verticalSegments = Mathf.Clamp(profile.verticalSegments, 1, 64);
            bool addThirdQuad = profile.useThirdQuad;


            var mesh = BuildXZCrossQuad(width, height, verticalSegments, addThirdQuad);
            mesh.name = $"{profile.name}_Grass_XZ_3D";

            string path = AssetDatabase.GetAssetPath(profile);
            string folder = System.IO.Path.GetDirectoryName(path);
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{mesh.name}.asset");

            AssetDatabase.CreateAsset(mesh, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Undo.RecordObject(profile, "Assign XZ Grass Mesh");
            profile.meshXZ = mesh;
            profile.SyncUseXZPlaneFromOrientation();
            EditorUtility.SetDirty(profile);

            SceneView.RepaintAll();
        }

        // XZ plane cards, rotate around Y for cross quads
        private static Mesh BuildXZCrossQuad(float width, float height, int verticalSegments, bool addThirdQuad)
        {
            verticalSegments = Mathf.Clamp(verticalSegments, 1, 64);
            int rows = verticalSegments + 1;

            int quadCount = addThirdQuad ? 3 : 2;
            float[] anglesDeg = addThirdQuad ? new[] { 0f, 90f, 45f } : new[] { 0f, 90f };

            int vertsPerQuad = rows * 2;
            int vCount = quadCount * vertsPerQuad;

            int indicesPerQuad = verticalSegments * 6;
            int iCount = quadCount * indicesPerQuad;

            var verts = new Vector3[vCount];
            var uvs = new Vector2[vCount];
            var normals = new Vector3[vCount];
            var tris = new int[iCount];

            int viBase = 0;
            int ti = 0;

            for (int q = 0; q < quadCount; q++)
            {
                Quaternion rot = Quaternion.Euler(0f, anglesDeg[q], 0f);
                Vector3 n = rot * Vector3.forward;

                for (int r = 0; r < rows; r++)
                {
                    float t = (float)r / verticalSegments;
                    float y = t * height;

                    Vector3 left = new Vector3(-width * 0.5f, y, 0f);
                    Vector3 right = new Vector3(width * 0.5f, y, 0f);

                    int vi = viBase + r * 2;

                    verts[vi + 0] = rot * left;
                    verts[vi + 1] = rot * right;

                    uvs[vi + 0] = new Vector2(0f, t);
                    uvs[vi + 1] = new Vector2(1f, t);

                    normals[vi + 0] = n;
                    normals[vi + 1] = n;
                }

                for (int s = 0; s < verticalSegments; s++)
                {
                    int row0 = s;
                    int row1 = s + 1;

                    int v00 = viBase + row0 * 2 + 0;
                    int v01 = viBase + row0 * 2 + 1;
                    int v10 = viBase + row1 * 2 + 0;
                    int v11 = viBase + row1 * 2 + 1;

                    tris[ti + 0] = v00;
                    tris[ti + 1] = v10;
                    tris[ti + 2] = v01;

                    tris[ti + 3] = v01;
                    tris[ti + 4] = v10;
                    tris[ti + 5] = v11;

                    ti += 6;
                }

                viBase += vertsPerQuad;
            }

            var mesh = new Mesh();
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.normals = normals;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
#endif
