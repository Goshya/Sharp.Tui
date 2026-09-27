using Sharp.Tui.Layout;

namespace Sharp.Tui.Tests.Layout;

public class SizeModeTests
{
    [Fact]
    public void Fixed_NegativeCells_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SizeMode.Fixed(-1));
    }

    [Fact]
    public void Fixed_Zero_DoesNotThrow()
    {
        SizeMode.Fixed(0);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Percent_OutOfRange_Throws(double percent)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SizeMode.Percent(percent));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(50.5)]
    public void Percent_InRange_DoesNotThrow(double percent)
    {
        SizeMode.Percent(percent);
    }

    [Fact]
    public void Fill_NegativeWeight_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SizeMode.Fill(-1));
    }

    [Fact]
    public void Fill_Zero_DoesNotThrow()
    {
        SizeMode.Fill(0);
    }

    [Fact]
    public void SameKindAndValue_AreEqual()
    {
        Assert.Equal(SizeMode.Fixed(10), SizeMode.Fixed(10));
        Assert.Equal(SizeMode.Percent(50), SizeMode.Percent(50));
        Assert.Equal(SizeMode.Fill(2), SizeMode.Fill(2));
    }

    [Fact]
    public void DifferentKinds_AreNeverEqual()
    {
        Assert.NotEqual(SizeMode.Fixed(1), SizeMode.Fill(1));
    }
}
