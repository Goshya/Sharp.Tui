using Sharp.Tui.Layout;

namespace Sharp.Tui.Tests.Layout;

public class ConstraintsTests
{
    [Fact]
    public void Constructor_MinWidthGreaterThanMaxWidth_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Constraints(10, 5, 0, 10));
    }

    [Fact]
    public void Constructor_MinHeightGreaterThanMaxHeight_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Constraints(0, 10, 10, 5));
    }

    [Fact]
    public void Constructor_MinEqualsMax_DoesNotThrow()
    {
        var constraints = new Constraints(5, 5, 5, 5);

        Assert.Equal(5, constraints.MinWidth);
        Assert.Equal(5, constraints.MaxWidth);
    }

    [Fact]
    public void Tight_PinsMinAndMaxToTheSameSize()
    {
        var constraints = Constraints.Tight(new Size(10, 20));

        Assert.Equal(new Constraints(10, 10, 20, 20), constraints);
    }

    [Fact]
    public void Loose_AllowsAnythingDownToZero()
    {
        var constraints = Constraints.Loose(new Size(10, 20));

        Assert.Equal(new Constraints(0, 10, 0, 20), constraints);
    }
}
