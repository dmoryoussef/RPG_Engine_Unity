using System;

namespace WorldPlacement.Runtime.Grid
{
    /// <summary>Pure C# integer cell coordinate.</summary>
    public readonly struct Cell2i : IEquatable<Cell2i>
    {
        public readonly int X;
        public readonly int Y;

        public Cell2i(int x, int y) { X = x; Y = y; }

        public bool Equals(Cell2i other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is Cell2i other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public override string ToString() => $"({X},{Y})";

        public static Cell2i operator +(Cell2i a, Cell2i b) => new Cell2i(a.X + b.X, a.Y + b.Y);
        public static Cell2i operator -(Cell2i a, Cell2i b) => new Cell2i(a.X - b.X, a.Y - b.Y);
    }
}
