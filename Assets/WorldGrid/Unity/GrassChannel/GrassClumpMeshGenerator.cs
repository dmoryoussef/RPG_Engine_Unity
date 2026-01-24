#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class GrassClumpMeshGenerator
{
    [MenuItem("Tools/Grass/Generate Cross-Quad Clump Mesh")]
    public static void Generate()
    {
        // --- Tweakables ---
        const float width = 0.35f;     // total quad width in local X
        const float height = 1.0f;     // quad height in local Y (base at 0)
        const bool addThirdQuad = true; // adds an extra quad at 45 degrees
        const string assetPath = "Assets/Meshes/Grass/SM_GrassClump_CrossQuads.asset";

        // Ensure folder exists
        EnsureFolders("Assets/Meshes", "Assets/Meshes/Grass");

        var mesh = BuildCrossQuadMesh(width, height, addThirdQuad);
        mesh.name = "SM_GrassClump_CrossQuads";

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

        Debug.Log($"Grass clump mesh generated at: {assetPath}");
    }

    private static Mesh BuildCrossQuadMesh(float width, float height, bool addThirdQuad)
    {
        // Each quad: 4 verts, 6 indices
        int quadCount = addThirdQuad ? 3 : 2;
        int vCount = quadCount * 4;
        int iCount = quadCount * 6;

        var verts = new Vector3[vCount];
        var uvs = new Vector2[vCount];
        var normals = new Vector3[vCount];
        var tris = new int[iCount];

        // A single upright quad centered on origin, base at y=0
        // We'll rotate it around Y to make the cross.
        Vector3 v0 = new Vector3(-width * 0.5f, 0f, 0f);
        Vector3 v1 = new Vector3(width * 0.5f, 0f, 0f);
        Vector3 v2 = new Vector3(-width * 0.5f, height, 0f);
        Vector3 v3 = new Vector3(width * 0.5f, height, 0f);

        // UVs: bottom-left, bottom-right, top-left, top-right
        Vector2 uv0 = new Vector2(0f, 0f);
        Vector2 uv1 = new Vector2(1f, 0f);
        Vector2 uv2 = new Vector2(0f, 1f);
        Vector2 uv3 = new Vector2(1f, 1f);

        float[] anglesDeg = addThirdQuad
            ? new[] { 0f, 90f, 45f }
            : new[] { 0f, 90f };

        int vi = 0;
        int ti = 0;

        for (int q = 0; q < quadCount; q++)
        {
            Quaternion rot = Quaternion.Euler(0f, anglesDeg[q], 0f);

            verts[vi + 0] = rot * v0;
            verts[vi + 1] = rot * v1;
            verts[vi + 2] = rot * v2;
            verts[vi + 3] = rot * v3;

            uvs[vi + 0] = uv0;
            uvs[vi + 1] = uv1;
            uvs[vi + 2] = uv2;
            uvs[vi + 3] = uv3;

            // Give each quad a normal that roughly faces outward so lighting behaves if you ever re-enable lights
            Vector3 n = rot * Vector3.forward;
            normals[vi + 0] = n;
            normals[vi + 1] = n;
            normals[vi + 2] = n;
            normals[vi + 3] = n;

            // Two triangles (0,2,1) (1,2,3)
            tris[ti + 0] = vi + 0;
            tris[ti + 1] = vi + 2;
            tris[ti + 2] = vi + 1;

            tris[ti + 3] = vi + 1;
            tris[ti + 4] = vi + 2;
            tris[ti + 5] = vi + 3;

            vi += 4;
            ti += 6;
        }

        var mesh = new Mesh
        {
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt16
        };

        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.normals = normals;
        mesh.triangles = tris;

        mesh.RecalculateBounds();
        // We intentionally do NOT recalc normals since we set them; if you prefer softer shading:
        // mesh.RecalculateNormals();

        return mesh;
    }

    private static void EnsureFolders(params string[] folders)
    {
        // Create nested folders if needed
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
