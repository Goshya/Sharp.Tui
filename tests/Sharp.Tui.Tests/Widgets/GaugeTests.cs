using Sharp.Tui.Layout;
using Sharp.Tui.Widgets;
using Buffer = Sharp.Tui.Core.Rendering.Buffer;

namespace Sharp.Tui.Tests.Widgets;

public class GaugeTests
{
    [Theory]
    [InlineData(-1.0, 0.0)]
    [InlineData(2.0, 1.0)]
    [InlineData(0.5, 0.5)]
    [InlineData(0.0, 0.0)]
    [InlineData(1.0, 1.0)]
    public void Constructor_ClampsFractionToZeroOneRange(double input, double expected)
    {
        Assert.Equal(expected, new Gauge(input).Fraction);
    }

    [Fact]
    public void Constructor_NaN_ClampsToZero()
    {
        // Math.Clamp alone leaves NaN untouched (every comparison with NaN is false) — this is
        // the one input Fraction's clamp needed a specific guard for, beyond what the issue's own
        // text asked for.
        Assert.Equal(0.0, new Gauge(double.NaN).Fraction);
    }

    [Fact]
    public void Measure_ReturnsTheSmallestAllowedSize_IgnoringContent()
    {
        var gauge = new Gauge(0.5, label: "does not affect Measure at all");

        Assert.Equal(new Size(3, 2), gauge.Measure(new Constraints(3, 100, 2, 100)));
    }

    [Fact]
    public void Render_LabelIsVerticallyCentered_WhenTallerThanOneRow()
    {
        var buffer = new Buffer(3, 3);

        new Gauge(1, label: "X").Render(buffer, new Rect(0, 0, 3, 3));

        Assert.Equal('X', (char)buffer[1, 1].Char.Value); // middle row, middle column
        Assert.NotEqual('X', (char)buffer[1, 0].Char.Value);
        Assert.NotEqual('X', (char)buffer[1, 2].Char.Value);
    }

    [Fact]
    public void Render_LabelLongerThanWidth_IsTruncatedNotElided()
    {
        var buffer = new Buffer(3, 1);

        new Gauge(1, label: "Way Too Long").Render(buffer, new Rect(0, 0, 3, 1));

        Assert.Equal('W', (char)buffer[0, 0].Char.Value);
        Assert.Equal('a', (char)buffer[1, 0].Char.Value);
        Assert.Equal('y', (char)buffer[2, 0].Char.Value);
    }
}
