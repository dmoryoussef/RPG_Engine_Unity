using NUnit.Framework;
using WorldGrid.Runtime.Math;

namespace WorldGrid.Tests.Math
{
    public sealed class MathUtilTests
    {
        [TestCase(-1, 32, -1)]
        [TestCase(-32, 32, -1)]
        [TestCase(-33, 32, -2)]
        [TestCase(0, 32, 0)]
        [TestCase(31, 32, 0)]
        [TestCase(32, 32, 1)]
        public void FloorDiv_Works(int a, int b, int expected)
        {
            Assert.That(MathUtil.FloorDiv(a, b), Is.EqualTo(expected));
        }

        [TestCase(-1, 32, 31)]
        [TestCase(-32, 32, 0)]
        [TestCase(-33, 32, 31)]
        [TestCase(0, 32, 0)]
        [TestCase(31, 32, 31)]
        [TestCase(32, 32, 0)]
        public void FloorMod_Works(int a, int b, int expected)
        {
            Assert.That(MathUtil.FloorMod(a, b), Is.EqualTo(expected));
        }
    }
}
