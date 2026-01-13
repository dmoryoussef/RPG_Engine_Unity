using System;

namespace WorldGrid.Runtime.Tiles
{
    /// <summary>
    /// Normalized UV rectangle (0..1). UMin/VMin is bottom-left, UMax/VMax top-right.
    /// </summary>
    public readonly struct RectUv : IEquatable<RectUv>
    {
        public readonly float UMin;
        public readonly float VMin;
        public readonly float UMax;
        public readonly float VMax;

        public RectUv(float uMin, float vMin, float uMax, float vMax)
        {
            UMin = uMin;
            VMin = vMin;
            UMax = uMax;
            VMax = vMax;
        }

        public bool Equals(RectUv other) =>
            UMin.Equals(other.UMin) && VMin.Equals(other.VMin) &&
            UMax.Equals(other.UMax) && VMax.Equals(other.VMax);

        public override bool Equals(object obj) => obj is RectUv other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(UMin, VMin, UMax, VMax);

        public static bool operator ==(RectUv a, RectUv b) => a.Equals(b);
        public static bool operator !=(RectUv a, RectUv b) => !a.Equals(b);

        public override string ToString() => $"RectUv({UMin},{VMin}..{UMax},{VMax})";
    }
}
