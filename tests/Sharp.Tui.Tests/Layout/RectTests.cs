using Sharp.Tui.Layout;

namespace Sharp.Tui.Tests.Layout;

public class RectTests
{
    [Fact]
    public void Intersect_OverlappingRects_ReturnsOverlap()
    {
        var a = new Rect(0, 0, 10, 10);
        var b = new Rect(5, 5, 10, 10);

        Assert.Equal(new Rect(5, 5, 5, 5), a.Intersect(b));
    }

    [Fact]
    public void Intersect_IsCommutative()
    {
        var a = new Rect(0, 0, 10, 10);
        var b = new Rect(5, 5, 10, 10);

        Assert.Equal(a.Intersect(b), b.Intersect(a));
    }

    [Fact]
    public void Intersect_NonOverlappingRects_ReturnsZeroArea()
    {
        var a = new Rect(0, 0, 5, 5);
        var b = new Rect(10, 10, 5, 5);

        var result = a.Intersect(b);

        Assert.Equal(0, result.Width);
        Assert.Equal(0, result.Height);
    }

    [Fact]
    public void Intersect_OneContainsTheOther_ReturnsTheSmallerOne()
    {
        var outer = new Rect(0, 0, 20, 20);
        var inner = new Rect(5, 5, 5, 5);

        Assert.Equal(inner, outer.Intersect(inner));
    }

    [Fact]
    public void Intersect_SameRect_ReturnsItself()
    {
        var rect = new Rect(3, 4, 10, 6);

        Assert.Equal(rect, rect.Intersect(rect));
    }

    [Fact]
    public void Intersect_AdjacentRects_ReturnsZeroArea()
    {
        // Touching edges (b starts exactly where a ends) — no actual overlapping cells.
        var a = new Rect(0, 0, 10, 10);
        var b = new Rect(10, 0, 10, 10);

        var result = a.Intersect(b);

        Assert.Equal(0, result.Width);
        Assert.Equal(10, result.Height);
    }
}
