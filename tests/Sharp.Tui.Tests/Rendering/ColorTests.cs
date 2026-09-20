using Sharp.Tui.Core.Rendering;

namespace Sharp.Tui.Tests.Rendering;

public class ColorTests
{
    [Fact]
    public void Default_HasDefaultKind_AndEqualsDefaultStruct()
    {
        Assert.Equal(ColorKind.Default, Color.Default.Kind);
        Assert.Equal(default(Color), Color.Default);
    }

    [Fact]
    public void Factories_SetTheMatchingKind()
    {
        Assert.Equal(ColorKind.Named16, Color.Named(3).Kind);
        Assert.Equal(ColorKind.Indexed256, Color.Indexed(200).Kind);
        Assert.Equal(ColorKind.Rgb, Color.Rgb(1, 2, 3).Kind);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(15)]
    public void Index_OfNamedColor_ReturnsTheNamedIndex(byte index)
    {
        Assert.Equal(index, Color.Named(index).Index);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    [InlineData(196)]
    [InlineData(255)]
    public void Index_OfIndexedColor_ReturnsTheIndex(byte index)
    {
        Assert.Equal(index, Color.Indexed(index).Index);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 2, 3)]
    [InlineData(255, 128, 7)]
    public void RgbComponents_OfRgbColor_ReturnTheGivenChannels(byte r, byte g, byte b)
    {
        var color = Color.Rgb(r, g, b);

        Assert.Equal(r, color.R);
        Assert.Equal(g, color.G);
        Assert.Equal(b, color.B);
    }

    [Fact]
    public void Accessors_OfDefaultColor_AreZero()
    {
        Assert.Equal(0, Color.Default.Index);
        Assert.Equal(0, Color.Default.R);
        Assert.Equal(0, Color.Default.G);
        Assert.Equal(0, Color.Default.B);
    }

    [Fact]
    public void Equals_SameKindAndValues_IsTrue()
    {
        Assert.Equal(Color.Named(3), Color.Named(3));
        Assert.Equal(Color.Indexed(200), Color.Indexed(200));
        Assert.Equal(Color.Rgb(1, 2, 3), Color.Rgb(1, 2, 3));
    }

    [Fact]
    public void Equals_SameNumericValueButDifferentKind_IsFalse()
    {
        Assert.NotEqual(Color.Named(5), Color.Indexed(5));
        Assert.NotEqual(Color.Indexed(5), Color.Rgb(5, 0, 0));
        Assert.NotEqual(Color.Named(5), Color.Rgb(5, 0, 0));
    }

    [Fact]
    public void Equals_DefaultIsNotEqualToNamedZero()
    {
        Assert.NotEqual(Color.Default, Color.Named(0));
    }

    [Theory]
    [InlineData(9, 2, 3)]
    [InlineData(1, 9, 3)]
    [InlineData(1, 2, 9)]
    public void Equals_RgbDifferingInAnySingleChannel_IsFalse(byte r, byte g, byte b)
    {
        Assert.NotEqual(Color.Rgb(1, 2, 3), Color.Rgb(r, g, b));
    }

    [Fact]
    public void Equals_Object_HandlesBoxedColorsAndForeignTypes()
    {
        object boxed = Color.Rgb(1, 2, 3);

        Assert.True(Color.Rgb(1, 2, 3).Equals(boxed));
        Assert.False(Color.Rgb(1, 2, 3).Equals((object)Color.Rgb(3, 2, 1)));
        Assert.False(Color.Rgb(1, 2, 3).Equals("not a color"));
        Assert.False(Color.Rgb(1, 2, 3).Equals(null));
    }

    [Fact]
    public void GetHashCode_EqualColors_ProduceEqualHashCodes()
    {
        Assert.Equal(Color.Named(3).GetHashCode(), Color.Named(3).GetHashCode());
        Assert.Equal(Color.Indexed(200).GetHashCode(), Color.Indexed(200).GetHashCode());
        Assert.Equal(Color.Rgb(1, 2, 3).GetHashCode(), Color.Rgb(1, 2, 3).GetHashCode());
        Assert.Equal(Color.Default.GetHashCode(), default(Color).GetHashCode());
    }
}
