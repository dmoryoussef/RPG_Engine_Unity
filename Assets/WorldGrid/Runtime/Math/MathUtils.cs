using System;

namespace WorldGrid.Runtime.Math
{
    public static class MathUtil
    {
        /// <summary>
        /// Floor division for integers (b must be > 0).
        /// Example: FloorDiv(-1, 32) == -1, FloorDiv(-33, 32) == -2
        /// </summary>
        public static int FloorDiv(int a, int b)
        {
            if (b <= 0) throw new ArgumentOutOfRangeException(nameof(b), "b must be > 0");

            // Truncating division in C# rounds toward zero.
            int q = a / b;
            int r = a % b;

            // If remainder is non-zero and signs differ, subtract 1 to round down.
            if (r != 0 && a < 0)
                q -= 1;

            return q;
        }

        /// <summary>
        /// Floor modulus for integers (b must be > 0).
        /// Result is always in [0, b-1].
        /// Example: FloorMod(-1, 32) == 31
        /// </summary>
        public static int FloorMod(int a, int b)
        {
            if (b <= 0) throw new ArgumentOutOfRangeException(nameof(b), "b must be > 0");

            int m = a % b;
            if (m < 0) m += b;
            return m;
        }
    }
}
