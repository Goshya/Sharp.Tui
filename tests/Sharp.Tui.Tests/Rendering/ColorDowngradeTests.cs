using Sharp.Tui.Core.Rendering;

namespace Sharp.Tui.Tests.Rendering;

public class ColorDowngradeTests
{
    [Theory]
    [InlineData(ColorSupport.NoColor)]
    [InlineData(ColorSupport.Named16)]
    [InlineData(ColorSupport.Indexed256)]
    [InlineData(ColorSupport.TrueColor)]
    public void DefaultColor_StaysDefault_AtEveryLevel(ColorSupport support)
    {
        Assert.Equal(Color.Default, Color.Default.Downgrade(support));
    }

    [Fact]
    public void NoColor_DropsEveryColorKindToDefault()
    {
        Assert.Equal(Color.Default, Color.Named(3).Downgrade(ColorSupport.NoColor));
        Assert.Equal(Color.Default, Color.Indexed(200).Downgrade(ColorSupport.NoColor));
        Assert.Equal(Color.Default, Color.Rgb(1, 2, 3).Downgrade(ColorSupport.NoColor));
    }

    [Fact]
    public void TrueColor_LeavesEveryColorKindUnchanged()
    {
        Assert.Equal(Color.Named(3), Color.Named(3).Downgrade(ColorSupport.TrueColor));
        Assert.Equal(Color.Indexed(200), Color.Indexed(200).Downgrade(ColorSupport.TrueColor));
        Assert.Equal(Color.Rgb(1, 2, 3), Color.Rgb(1, 2, 3).Downgrade(ColorSupport.TrueColor));
    }

    [Fact]
    public void Indexed256_LeavesNamedAndIndexedUnchanged()
    {
        Assert.Equal(Color.Named(3), Color.Named(3).Downgrade(ColorSupport.Indexed256));
        Assert.Equal(Color.Indexed(100), Color.Indexed(100).Downgrade(ColorSupport.Indexed256));
    }

    [Theory]
    [InlineData(255, 0, 0, 196)]
    [InlineData(0, 255, 0, 46)]
    [InlineData(0, 0, 255, 21)]
    [InlineData(0, 0, 0, 16)]
    [InlineData(255, 255, 255, 231)]
    [InlineData(95, 135, 175, 67)]     // exact cube levels (1, 2, 3)
    [InlineData(128, 128, 128, 244)]   // mid-grey lands exactly on the grayscale ramp (8 + 10 * 12)
    [InlineData(8, 8, 8, 232)]         // darkest grayscale step beats the near-black cube entry
    public void Indexed256_MapsRgbToNearestCubeOrGreyEntry(byte r, byte g, byte b, byte expectedIndex)
    {
        var result = Color.Rgb(r, g, b).Downgrade(ColorSupport.Indexed256);

        Assert.Equal(Color.Indexed(expectedIndex), result);
    }

    [Fact]
    public void Named16_LeavesNamedUnchanged()
    {
        Assert.Equal(Color.Named(3), Color.Named(3).Downgrade(ColorSupport.Named16));
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(205, 0, 0, 1)]
    [InlineData(255, 0, 0, 9)]
    [InlineData(0, 255, 0, 10)]
    [InlineData(127, 127, 127, 8)]
    [InlineData(250, 250, 250, 15)]    // near-white is closer to bright white than to light grey (229)
    [InlineData(255, 255, 255, 15)]
    public void Named16_MapsRgbToNearestAnsiColor(byte r, byte g, byte b, byte expectedIndex)
    {
        var result = Color.Rgb(r, g, b).Downgrade(ColorSupport.Named16);

        Assert.Equal(Color.Named(expectedIndex), result);
    }

    [Theory]
    [InlineData(5, 5)]                 // 0-15 are already ANSI colors
    [InlineData(15, 15)]
    [InlineData(16, 0)]                // cube (0, 0, 0)
    [InlineData(196, 9)]               // cube (255, 0, 0)
    [InlineData(231, 15)]              // cube (255, 255, 255)
    [InlineData(244, 8)]               // grey 128
    [InlineData(232, 0)]               // grey 8, nearest to black
    public void Named16_MapsIndexedColorToNearestAnsiColor(byte index, byte expectedNamed)
    {
        var result = Color.Indexed(index).Downgrade(ColorSupport.Named16);

        Assert.Equal(Color.Named(expectedNamed), result);
    }

    [Fact]
    public void RgbSweep_AlwaysProducesAColorValidForTheTargetLevel()
    {
        for (var r = 0; r <= 255; r += 51)
        for (var g = 0; g <= 255; g += 51)
        for (var b = 0; b <= 255; b += 51)
        {
            var rgb = Color.Rgb((byte)r, (byte)g, (byte)b);

            var indexed = rgb.Downgrade(ColorSupport.Indexed256);
            Assert.Contains(Enumerable.Range(16, 240), i => Color.Indexed((byte)i).Equals(indexed));

            var named = rgb.Downgrade(ColorSupport.Named16);
            Assert.Contains(Enumerable.Range(0, 16), i => Color.Named((byte)i).Equals(named));
        }
    }

    [Fact]
    public void UndefinedColorSupport_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Color.Rgb(1, 2, 3).Downgrade((ColorSupport)99));
    }
}
