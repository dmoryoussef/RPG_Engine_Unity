using UnityEngine;

namespace Grass
{
    public enum GrassViewMode { TopDown = 0, SideView = 1 }

    public struct GrassInfluencer
    {
        public Vector3 position;
        public float radius;
        public float strength;
    }

    public struct GrassInstance
    {
        public Vector3 position;
        public float rotationRad;
        public float scale;
        public Color tint;
    }

    public readonly struct PatchId
    {
        public readonly int x;
        public readonly int y;

        public PatchId(int x, int y) { this.x = x; this.y = y; }

        public override int GetHashCode()
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + x;
                h = h * 31 + y;
                return h;
            }
        }

        public override bool Equals(object obj) => obj is PatchId other && other.x == x && other.y == y;

        public override string ToString() => $"({x},{y})";
    }
}
