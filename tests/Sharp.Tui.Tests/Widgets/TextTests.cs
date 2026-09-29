using Sharp.Tui.Layout;
using Sharp.Tui.Widgets;
using Buffer = Sharp.Tui.Core.Rendering.Buffer;

namespace Sharp.Tui.Tests.Widgets;

public class TextTests
{
    private static string Row(Buffer buffer, int y)
    {
        var chars = new char[buffer.Width];
        for (var x = 0; x < buffer.Width; x++)
            chars[x] = (char)buffer[x, y].Char.Value;

        return new string(chars);
    }

    [Fact]
    public void Render_WritesTextAtAreaOrigin()
    {
        var buffer = new Buffer(10, 3);

        new Text("hi").Render(buffer, new Rect(2, 1, 6, 2));

        Assert.Equal("          ", Row(buffer, 0));
        Assert.Equal("  hi      ", Row(buffer, 1));
    }

    [Fact]
    public void Render_ClipsToAreaWidth()
    {
        var buffer = new Buffer(10, 1);

        new Text("abcdef").Render(buffer, new Rect(1, 0, 3, 1));

        Assert.Equal(" abc      ", Row(buffer, 0));
    }

    [Fact]
    public void Render_NewlineStartsNextLineAtAreaLeft()
    {
        var buffer = new Buffer(6, 3);

        new Text("ab\ncd").Render(buffer, new Rect(1, 0, 4, 3));

        Assert.Equal(" ab   ", Row(buffer, 0));
        Assert.Equal(" cd   ", Row(buffer, 1));
    }

    [Fact]
    public void Render_ClipsToAreaHeight()
    {
        var buffer = new Buffer(4, 3);

        new Text("a\nb\nc").Render(buffer, new Rect(0, 0, 4, 2));

        Assert.Equal("a   ", Row(buffer, 0));
        Assert.Equal("b   ", Row(buffer, 1));
        Assert.Equal("    ", Row(buffer, 2));
    }

    [Fact]
    public void Render_AreaLargerThanBuffer_DoesNotThrow()
    {
        var buffer = new Buffer(3, 1);

        new Text("abcdef\nx").Render(buffer, new Rect(0, 0, 100, 100));

        Assert.Equal("abc", Row(buffer, 0));
    }

    [Fact]
    public void Render_EmptyOrEmptyArea_WritesNothing()
    {
        var buffer = new Buffer(3, 1);

        new Text("").Render(buffer, new Rect(0, 0, 3, 1));
        new Text("abc").Render(buffer, new Rect(0, 0, 0, 0));

        Assert.Equal("   ", Row(buffer, 0));
    }

    [Fact]
    public void Render_SupplementaryPlaneRune_TakesOneCell()
    {
        var buffer = new Buffer(4, 1);

        new Text("a😀b").Render(buffer, new Rect(0, 0, 4, 1));

        Assert.Equal(0x1F600, buffer[1, 0].Char.Value);
        Assert.Equal('b', buffer[2, 0].Char.Value);
    }

    [Fact]
    public void Render_CarriageReturnIsIgnored()
    {
        var buffer = new Buffer(4, 2);

        new Text("a\r\nb").Render(buffer, new Rect(0, 0, 4, 2));

        Assert.Equal("a   ", Row(buffer, 0));
        Assert.Equal("b   ", Row(buffer, 1));
    }

    // --- Measure ---------------------------------------------------------------------------------
    // Whatever Render actually draws is the ground truth: Measure must agree with it, not with a
    // simpler-but-wrong approximation (UTF-16 char count instead of Rune count, a naive
    // string.Split('\n') that doesn't special-case '\r', ...).

    private static readonly Constraints Loose = Constraints.Loose(new Size(1000, 1000));

    [Fact]
    public void Measure_SingleLine_WidthIsRuneCountHeightIsOne()
    {
        Assert.Equal(new Size(5, 1), new Text("hello").Measure(Loose));
    }

    [Fact]
    public void Measure_MultipleLines_WidthIsLongestLineHeightIsLineCount()
    {
        Assert.Equal(new Size(3, 3), new Text("ab\ncde\nf").Measure(Loose));
    }

    [Fact]
    public void Measure_EmptyContent_IsOneEmptyLine()
    {
        // An empty string is still one (blank) line, the same way a blank line in an editor
        // still occupies a row — not zero lines.
        Assert.Equal(new Size(0, 1), new Text("").Measure(Loose));
    }

    [Fact]
    public void Measure_NullContent_IsTreatedLikeEmptyContent()
    {
        // Render treats null the same as "" (Content ?? string.Empty) — Measure must agree.
        Assert.Equal(new Text("").Measure(Loose), new Text(null!).Measure(Loose));
    }

    [Fact]
    public void Measure_TrailingNewline_CountsAnExtraEmptyLine()
    {
        // Mirrors Render: after the '\n', the cursor has moved to a new line even though nothing
        // more was drawn on it — that line still logically exists and takes up height.
        Assert.Equal(new Size(1, 2), new Text("a\n").Measure(Loose));
    }

    [Fact]
    public void Measure_CarriageReturn_IsIgnoredNotCountedAsWidth()
    {
        // "a\r\nb": if '\r' weren't specifically skipped (e.g. a naive Split('\n') that leaves
        // the '\r' attached to the first line), the first line would wrongly measure as width 2.
        Assert.Equal(new Size(1, 2), new Text("a\r\nb").Measure(Loose));
    }

    [Fact]
    public void Measure_SupplementaryPlaneRune_CountsAsOneCellWide()
    {
        // Matches Render_SupplementaryPlaneRune_TakesOneCell: counting Runes, not UTF-16 chars
        // (the emoji is a surrogate pair — string.Length would wrongly report 4, not 3).
        Assert.Equal(new Size(3, 1), new Text("a😀b").Measure(Loose));
    }

    [Fact]
    public void Measure_ClampsDownToMaxWidthAndMaxHeight()
    {
        var constraints = new Constraints(0, 3, 0, 1);

        Assert.Equal(new Size(3, 1), new Text("hello\nworld").Measure(constraints));
    }

    [Fact]
    public void Measure_ClampsUpToMinWidthAndMinHeight()
    {
        var constraints = new Constraints(10, 20, 5, 5);

        Assert.Equal(new Size(10, 5), new Text("hi").Measure(constraints));
    }

    // --- Style ---------------------------------------------------------------------------------

    [Fact]
    public void Render_NoStyle_MatchesWhatWasHardcodedBeforeStyleExisted()
    {
        var buffer = new Buffer(1, 1);

        new Text("x").Render(buffer, new Rect(0, 0, 1, 1));

        var cell = buffer[0, 0];
        Assert.Equal(Sharp.Tui.Core.Rendering.Color.Default, cell.Foreground);
        Assert.Equal(Sharp.Tui.Core.Rendering.Color.Default, cell.Background);
        Assert.Equal(Sharp.Tui.Core.Rendering.StyleFlags.None, cell.Style);
    }

    [Fact]
    public void Render_WithStyle_ResolvedStyleReachesEveryCell()
    {
        var buffer = new Buffer(2, 1);
        var style = new Sharp.Tui.Core.Rendering.Style(
            Foreground: Sharp.Tui.Core.Rendering.Color.Named(1),
            Bold: true);

        new Text("ab", style).Render(buffer, new Rect(0, 0, 2, 1));

        foreach (var cell in new[] { buffer[0, 0], buffer[1, 0] })
        {
            Assert.Equal(Sharp.Tui.Core.Rendering.Color.Named(1), cell.Foreground);
            Assert.Equal(Sharp.Tui.Core.Rendering.StyleFlags.Bold, cell.Style);
        }
    }
}
