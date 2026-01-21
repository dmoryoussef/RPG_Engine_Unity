using System.Collections.Generic;
using UnityEngine;

namespace WorldGrid.Unity.Rendering
{
    public sealed class MeshData
    {
        public readonly List<Vector3> Vertices = new();
        public readonly List<Vector2> Uvs = new();
        public readonly List<int> Triangles = new();

        // NEW: vertex colors for tint/variation
        public readonly List<Color32> Colors = new();

        public void Clear()
        {
            Vertices.Clear();
            Uvs.Clear();
            Triangles.Clear();
            Colors.Clear();
        }
    }
}
