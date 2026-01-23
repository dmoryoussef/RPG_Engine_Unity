using NUnit.Framework;
using WorldGrid.Runtime.Tiles;

namespace WorldGrid.Tests.Tiles
{
    //public sealed class TileLibraryTests
    //{
    //    [Test]
    //    public void TryGetUv_ReturnsFalse_ForMissingTileId()
    //    {
    //        var lib = new TileLibrary();
    //        Assert.That(lib.TryGetUv(123, out _), Is.False);
    //    }

    //    [Test]
    //    public void Set_Then_TryGetUv_ReturnsUv()
    //    {
    //        var lib = new TileLibrary();
    //        var uv = new RectUv(0.0f, 0.0f, 0.25f, 0.25f);

    //        lib.Set(new TileDef(tileId: 7, name: "Grass", uv: uv));

    //        Assert.That(lib.TryGetUv(7, out var got), Is.True);
    //        Assert.That(got, Is.EqualTo(uv));
    //    }

    //    [Test]
    //    public void Tags_AreNormalized_Distinct_Trimmed()
    //    {
    //        var lib = new TileLibrary();
    //        lib.Set(new TileDef(
    //            tileId: 1,
    //            name: "Water",
    //            uv: new RectUv(0, 0, 1, 1),
    //            tags: new[] { " liquid", "Liquid", "", "  ", "hazard" }
    //        ));

    //        var tags = lib.GetTagsOrEmpty(1);
    //        Assert.That(tags.Count, Is.EqualTo(2));
    //        Assert.That(tags, Does.Contain("liquid"));
    //        Assert.That(tags, Does.Contain("hazard"));
    //    }

    //    [Test]
    //    public void TileLibrary_ToString_LooksUpOrReportsUnknown()
    //    {
    //        var lib = new TileLibrary();
    //        Assert.That(lib.ToDebugString(42), Is.EqualTo("<unknown tileId=42>"));

    //        lib.Set(new TileDef(42, "Stone", new RectUv(0, 0, 0.5f, 0.5f), new[] { "solid" }));
    //        var s = lib.ToDebugString(42);

    //        Assert.That(s, Does.Contain("Stone"));
    //        Assert.That(s, Does.Contain("id=42"));
    //        Assert.That(s, Does.Contain("solid"));
    //    }
    //}
}
