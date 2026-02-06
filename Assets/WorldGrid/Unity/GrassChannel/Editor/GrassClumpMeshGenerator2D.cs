#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Grass.Editor
{
    public static class GrassClumpMeshGenerator2D
    {
        // 2D defaults: small cross angle keeps blades mostly upright.
        // If you want to expose these in the profile later, we will.
        private const float DefaultCrossAngleDeg = 20f; // try 15..30
        private const int DefaultQuadCount = 2;         // 1,2,or 3

        public static void GenerateIntoProfile(Grass.GrassRenderProfile profile)
        {
            int verticalSegments = Mathf.Clamp(profile.verticalSegments, 1, 64);

            const float width = 0.35f;
            const float height = 1.0f;

            float crossAngleDeg = DefaultCrossAngleDeg;
            int quadCount = DefaultQuadCount;

            var mesh = BuildXYClump(width, height, verticalSegments, quadCount, crossAngleDeg);
            mesh.name = $"{profile.name}_Grass_XY_2D";

            string path = AssetDatabase.GetAssetPath(profile);
            string folder = System.IO.Path.GetDirectoryName(path);
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{mesh.name}.asset");

            AssetDatabase.CreateAsset(mesh, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Undo.RecordObject(profile, "Assign XY Grass Mesh");
            profile.meshXY = mesh;
            profile.SyncUseXZPlaneFromOrientation();
            EditorUtility.SetDirty(profile);

            SceneView.RepaintAll();
        }

        /// <summary>
        /// All quads lie in XY plane (z=0), normals +Z.
        /// quadCount:
        ///   1 => angle 0
        ///   2 => angles -a/2, +a/2
        ///   3 => angles -a, 0, +a
        /// crossAngleDeg (a): use 15..30 for 2D. (90 is the 3D cross trick and looks sideways in 2D.)
        /// </summary>
        private static Mesh BuildXYClump(float width, float height, int verticalSegments, int quadCount, float crossAngleDeg)
        {
            verticalSegments = Mathf.Max(1, verticalSegments);
            quadCount = Mathf.Clamp(quadCount, 1, 3);

            float[] anglesDeg;
            if (quadCount == 1)
                anglesDeg = new[] { 0f };
            else if (quadCount == 2)
                anglesDeg = new[] { -crossAngleDeg * 0.5f, +crossAngleDeg * 0.5f };
            else
                anglesDeg = new[] { -crossAngleDeg, 0f, +crossAngleDeg };

            int rows = verticalSegments + 1;
            int vertsPerQuad = rows * 2;

            int vCount = anglesDeg.Length * vertsPerQuad;
            int iCount = anglesDeg.Length * verticalSegments * 6;

            var verts = new Vector3[vCount];
            var uvs = new Vector2[vCount];
            var normals = new Vector3[vCount];
            var tris = new int[iCount];

            int viBase = 0;
            int ti = 0;

            for (int q = 0; q < anglesDeg.Length; q++)
            {
                Quaternion rot = Quaternion.Euler(0f, 0f, anglesDeg[q]);
                Vector3 normal = Vector3.forward;

                for (int r = 0; r < rows; r++)
                {
                    float t = (float)r / verticalSegments;
                    float y = t * height;

                    Vector3 left = new Vector3(-width * 0.5f, y, 0f);
                    Vector3 right = new Vector3(+width * 0.5f, y, 0f);

                    int vi = viBase + r * 2;

                    verts[vi + 0] = rot * left;
                    verts[vi + 1] = rot * right;

                    uvs[vi + 0] = new Vector2(0f, t);
                    uvs[vi + 1] = new Vector2(1f, t);

                    normals[vi + 0] = normal;
                    normals[vi + 1] = normal;
                }

                for (int s = 0; s < verticalSegments; s++)
                {
                    int v00 = viBase + s * 2;
                    int v01 = v00 + 1;
                    int v10 = v00 + 2;
                    int v11 = v00 + 3;

                    tris[ti++] = v00;
                    tris[ti++] = v10;
                    tris[ti++] = v01;

                    tris[ti++] = v01;
                    tris[ti++] = v10;
                    tris[ti++] = v11;
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
