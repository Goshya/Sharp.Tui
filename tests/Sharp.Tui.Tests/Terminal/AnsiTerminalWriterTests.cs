using System.Text;
using Sharp.Tui.Core.Rendering;
using Sharp.Tui.Core.Terminal;

namespace Sharp.Tui.Tests.Terminal;

public class AnsiTerminalWriterTests
{
    private const string Esc = "";

    // MemoryStream.Write(ReadOnlySpan<byte>) forwards to this overload in derived types,
    // so counting here covers both ways of writing without double-counting.
    private sealed class CountingStream : MemoryStream
    {
        public int WriteCalls { get; private set; }

        public override void Write(byte[] buffer, int offset, int count)
        {
            WriteCalls++;
            base.Write(buffer, offset, count);
        }
    }

    private static string Text(MemoryStream stream) => Encoding.ASCII.GetString(stream.ToArray());

    [Fact]
    public void Constructor_NullStream_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new AnsiTerminalWriter(null!, ColorSupport.TrueColor));
    }

    [Fact]
    public void Constructor_NonWritableStream_Throws()
    {
        using var readOnly = new MemoryStream(new byte[1], writable: false);

        Assert.Throws<ArgumentException>(() => new AnsiTerminalWriter(readOnly, ColorSupport.TrueColor));
    }

    [Fact]
    public void Flush_WithNothingBuffered_WritesNothing()
    {
        var stream = new CountingStream();
        var writer = new AnsiTerminalWriter(stream, ColorSupport.TrueColor);

        writer.Flush();

        Assert.Equal(0, stream.WriteCalls);
        Assert.Equal(0, stream.Length);
    }

    [Fact]
    public void MoveCursor_IsNotWrittenUntilFlush()
    {
        var stream = new MemoryStream();
        var writer = new AnsiTerminalWriter(stream, ColorSupport.TrueColor);

        writer.MoveCursor(0, 0);
        Assert.Equal(0, stream.Length);

        writer.Flush();
        Assert.Equal($"{Esc}[1;1H", Text(stream));
    }

    [Theory]
    [InlineData(0, 0, "[1;1H")]
    [InlineData(9, 2, "[3;10H")]
    [InlineData(199, 49, "[50;200H")]
    public void MoveCursor_EmitsOneBasedRowAndColumn(int x, int y, string expectedTail)
    {
        var stream = new MemoryStream();
        var writer = new AnsiTerminalWriter(stream, ColorSupport.TrueColor);

        writer.MoveCursor(x, y);
        writer.Flush();

        Assert.Equal(Esc + expectedTail, Text(stream));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void MoveCursor_NegativeCoordinate_Throws(int x, int y)
    {
        var writer = new AnsiTerminalWriter(new MemoryStream(), ColorSupport.TrueColor);

        Assert.Throws<ArgumentOutOfRangeException>(() => writer.MoveCursor(x, y));
    }

    [Fact]
    public void Flush_SendsEverythingBufferedInASingleWrite()
    {
        var stream = new CountingStream();
        var writer = new AnsiTerminalWriter(stream, ColorSupport.TrueColor);

        writer.MoveCursor(0, 0);
        writer.MoveCursor(1, 1);
        writer.MoveCursor(2, 2);
        writer.Flush();

        Assert.Equal(1, stream.WriteCalls);
        Assert.Equal($"{Esc}[1;1H{Esc}[2;2H{Esc}[3;3H", Text(stream));
    }

    [Fact]
    public void Flush_ClearsTheBuffer_SoNothingIsWrittenTwice()
    {
        var stream = new MemoryStream();
        var writer = new AnsiTerminalWriter(stream, ColorSupport.TrueColor);

        writer.MoveCursor(0, 0);
        writer.Flush();
        writer.Flush();

        Assert.Equal($"{Esc}[1;1H", Text(stream));
    }

    [Fact]
    public void Buffer_GrowsBeyondItsInitialCapacity_WithoutLosingBytes()
    {
        var stream = new MemoryStream();
        var writer = new AnsiTerminalWriter(stream, ColorSupport.TrueColor);
        var expected = new StringBuilder();

        for (var i = 0; i < 2000; i++)
        {
            writer.MoveCursor(i % 200, i % 50);
            expected.Append($"{Esc}[{i % 50 + 1};{i % 200 + 1}H");
        }
        writer.Flush();

        Assert.True(expected.Length > 4096);
        Assert.Equal(expected.ToString(), Text(stream));
    }
}
