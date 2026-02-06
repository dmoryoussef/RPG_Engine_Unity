#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class GrassClumpMeshGenerator
{
    [MenuItem("Tools/Grass/Generate Subdivided Cross-Quad Clump Mesh")]
    public static void Generate()
    {
        // --- Tweakables ---
        const float width = 0.35f;         // blade card width
        const float height = 1.0f;         // blade height (local +Y)
        const int verticalSegments = 8;    // <-- increase for smoother bending (6-12 recommended)
        const bool addThirdQuad = true;    // optional extra quad at 45 degrees
        const string assetPath = "Assets/Meshes/Grass/SM_GrassClump_Subdivided.asset";

        EnsureFolders("Assets/Meshes", "Assets/Meshes/Grass");

        var mesh = BuildCrossQuadMeshSubdivided(width, height, verticalSegments, addThirdQuad);
        mesh.name = "SM_GrassClump_Subdivided";

        // Create/replace asset
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
        if (existing != null)
        {
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            mesh = existing;
        }
        else
        {
            AssetDatabase.CreateAsset(mesh, assetPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorGUIUtility.PingObject(mesh);
        Selection.activeObject = mesh;

        Debug.Log($"Subdivided grass clump mesh generated at: {assetPath}");
    }

    private static Mesh BuildCrossQuadMeshSubdivided(float width, float height, int verticalSegments, bool addThirdQuad)
    {
        verticalSegments = Mathf.Clamp(verticalSegments, 1, 64);

        // Each quad becomes a grid: 2 columns (left/right) x (verticalSegments+1) rows
        int rows = verticalSegments + 1;
        int vertsPerQuad = rows * 2;

        int quadCount = addThirdQuad ? 3 : 2;
        int vCount = quadCount * vertsPerQuad;

        // Each vertical segment adds 2 triangles => 6 indices per segment
        int indicesPerQuad = verticalSegments * 6;
        int iCount = quadCount * indicesPerQuad;

        var verts = new Vector3[vCount];
        var uvs = new Vector2[vCount];
        var normals = new Vector3[vCount];
        var tris = new int[iCount];

        float[] anglesDeg = addThirdQuad
            ? new[] { 0f, 90f, 45f }
            : new[] { 0f, 90f };

        int viBase = 0;
        int ti = 0;

        for (int q = 0; q < quadCount; q++)
        {
            Quaternion rot = Quaternion.Euler(0f, anglesDeg[q], 0f);
            Vector3 n = rot * Vector3.forward;

            // Build vertices
            for (int r = 0; r < rows; r++)
            {
                float t = (float)r / verticalSegments; // 0..1
                float y = t * height;

                // left/right points along X in local quad space
                Vector3 left = new Vector3(-width * 0.5f, y, 0f);
                Vector3 right = new Vector3(width * 0.5f, y, 0f);

                int vi = viBase + r * 2;

                verts[vi + 0] = rot * left;
                verts[vi + 1] = rot * right;

                // UVs: U across width, V along height
                uvs[vi + 0] = new Vector2(0f, t);
                uvs[vi + 1] = new Vector2(1f, t);

                normals[vi + 0] = n;
                normals[vi + 1] = n;
            }

            // Build triangles per vertical segment
            for (int s = 0; s < verticalSegments; s++)
            {
                int row0 = s;
                int row1 = s + 1;

                int v00 = viBase + row0 * 2 + 0; // left lower
                int v01 = viBase + row0 * 2 + 1; // right lower
                int v10 = viBase + row1 * 2 + 0; // left upper
                int v11 = viBase + row1 * 2 + 1; // right upper

                // Two triangles: (v00, v10, v01) and (v01, v10, v11)
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

        var mesh = new Mesh
        {
            indexFormat = (vCount > 65535)
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16
        };

        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.normals = normals;
        mesh.triangles = tris;

        mesh.RecalculateBounds();
        return mesh;
    }

    private static void EnsureFolders(params string[] folders)
    {
        for (int i = 0; i < folders.Length; i++)
        {
            string path = folders[i];
            if (AssetDatabase.IsValidFolder(path)) continue;

            int slash = path.LastIndexOf('/');
            if (slash <= 0) continue;

            string parent = path.Substring(0, slash);
            string name = path.Substring(slash + 1);

            if (!AssetDatabase.IsValidFolder(parent))
                continue;

            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
