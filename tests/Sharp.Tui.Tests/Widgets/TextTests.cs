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
}
