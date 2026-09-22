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

    private static string Utf8Text(MemoryStream stream) => Encoding.UTF8.GetString(stream.ToArray());

    // A write sink with no backing storage, so an allocation test measures only what
    // AnsiTerminalWriter itself allocates, not whatever the destination stream might.
    private sealed class DiscardingStream : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) { }
        public override void Write(ReadOnlySpan<byte> buffer) { }
    }

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
    public void SetStyle_IsNotWrittenUntilFlush()
    {
        var stream = new MemoryStream();
        var writer = new AnsiTerminalWriter(stream, ColorSupport.TrueColor);

        writer.SetStyle(Color.Default, Color.Default, StyleFlags.None);
        Assert.Equal(0, stream.Length);

        writer.Flush();
        Assert.Equal($"{Esc}[0m", Text(stream));
    }

    [Theory]
    [InlineData(StyleFlags.None, "[0m")]
    [InlineData(StyleFlags.Bold, "[0;1m")]
    [InlineData(StyleFlags.Italic, "[0;3m")]
    [InlineData(StyleFlags.Underline, "[0;4m")]
    [InlineData(StyleFlags.Strikethrough, "[0;9m")]
    [InlineData(StyleFlags.Bold | StyleFlags.Underline, "[0;1;4m")]
    [InlineData(StyleFlags.Bold | StyleFlags.Italic | StyleFlags.Underline | StyleFlags.Strikethrough, "[0;1;3;4;9m")]
    public void SetStyle_EmitsStyleFlagsInFixedOrder(StyleFlags style, string expectedTail)
    {
        var stream = new MemoryStream();
        var writer = new AnsiTerminalWriter(stream, ColorSupport.TrueColor);

        writer.SetStyle(Color.Default, Color.Default, style);
        writer.Flush();

        Assert.Equal(Esc + expectedTail, Text(stream));
    }

    [Theory]
    [InlineData((byte)0, "[0;30m")]
    [InlineData((byte)7, "[0;37m")]
    [InlineData((byte)8, "[0;90m")]
    [InlineData((byte)15, "[0;97m")]
    public void SetStyle_Named16Foreground_EmitsStandardOrBrightCode(byte index, string expectedTail)
    {
        var stream = new MemoryStream();
        var writer = new AnsiTerminalWriter(stream, ColorSupport.TrueColor);

        writer.SetStyle(Color.Named(index), Color.Default, StyleFlags.None);
        writer.Flush();

        Assert.Equal(Esc + expectedTail, Text(stream));
    }

    [Theory]
    [InlineData((byte)0, "[0;40m")]
    [InlineData((byte)7, "[0;47m")]
    [InlineData((byte)8, "[0;100m")]
    [InlineData((byte)15, "[0;107m")]
    public void SetStyle_Named16Background_EmitsStandardOrBrightCode(byte index, string expectedTail)
    {
        var stream = new MemoryStream();
        var writer = new AnsiTerminalWriter(stream, ColorSupport.TrueColor);

        writer.SetStyle(Color.Default, Color.Named(index), StyleFlags.None);
        writer.Flush();

        Assert.Equal(Esc + expectedTail, Text(stream));
    }

    [Fact]
    public void SetStyle_Indexed256Foreground_EmitsExtendedForegroundCode()
    {
        var stream = new MemoryStream();
        var writer = new AnsiTerminalWriter(stream, ColorSupport.TrueColor);

        writer.SetStyle(Color.Indexed(200), Color.Default, StyleFlags.None);
        writer.Flush();

        Assert.Equal($"{Esc}[0;38;5;200m", Text(stream));
    }

    [Fact]
    public void SetStyle_Indexed256Background_EmitsExtendedBackgroundCode()
    {
        var stream = new MemoryStream();
        var writer = new AnsiTerminalWriter(stream, ColorSupport.TrueColor);

        writer.SetStyle(Color.Default, Color.Indexed(200), StyleFlags.None);
        writer.Flush();

        Assert.Equal($"{Esc}[0;48;5;200m", Text(stream));
    }

    [Fact]
    public void SetStyle_RgbForeground_EmitsTrueColorForegroundCode()
    {
        var stream = new MemoryStream();
        var writer = new AnsiTerminalWriter(stream, ColorSupport.TrueColor);

        writer.SetStyle(Color.Rgb(10, 20, 30), Color.Default, StyleFlags.None);
        writer.Flush();

        Assert.Equal($"{Esc}[0;38;2;10;20;30m", Text(stream));
    }

    [Fact]
    public void SetStyle_RgbBackground_EmitsTrueColorBackgroundCode()
    {
        var stream = new MemoryStream();
        var writer = new AnsiTerminalWriter(stream, ColorSupport.TrueColor);

        writer.SetStyle(Color.Default, Color.Rgb(10, 20, 30), StyleFlags.None);
        writer.Flush();

        Assert.Equal($"{Esc}[0;48;2;10;20;30m", Text(stream));
    }

    [Fact]
    public void SetStyle_ForegroundBackgroundAndFlagsTogether_EmitsAllInOneSequence()
    {
        var stream = new MemoryStream();
        var writer = new AnsiTerminalWriter(stream, ColorSupport.TrueColor);

        writer.SetStyle(Color.Rgb(255, 0, 0), Color.Named(4), StyleFlags.Bold | StyleFlags.Underline);
        writer.Flush();

        Assert.Equal($"{Esc}[0;1;4;38;2;255;0;0;44m", Text(stream));
    }

    [Fact]
    public void SetStyle_NoColorSupport_DowngradesColorsToDefault_ButKeepsStyleFlags()
    {
        var stream = new MemoryStream();
        var writer = new AnsiTerminalWriter(stream, ColorSupport.NoColor);

        writer.SetStyle(Color.Rgb(1, 2, 3), Color.Indexed(50), StyleFlags.Bold);
        writer.Flush();

        Assert.Equal($"{Esc}[0;1m", Text(stream));
    }

    [Fact]
    public void Write_IsNotWrittenUntilFlush()
    {
        var stream = new MemoryStream();
        var writer = new AnsiTerminalWriter(stream, ColorSupport.TrueColor);

        writer.Write(new Rune('A'));
        Assert.Equal(0, stream.Length);

        writer.Flush();
        Assert.Equal("A", Utf8Text(stream));
    }

    [Theory]
    [InlineData(0x0041, 1, "A")]   // 1-byte: ASCII
    [InlineData(0x042F, 2, "Я")]   // 2-byte: Cyrillic
    [InlineData(0x20AC, 3, "€")]   // 3-byte: Euro sign
    [InlineData(0x1F600, 4, "😀")] // 4-byte: outside the BMP
    public void Write_EncodesRuneAsUtf8(int codePoint, int expectedByteCount, string expectedText)
    {
        var stream = new MemoryStream();
        var writer = new AnsiTerminalWriter(stream, ColorSupport.TrueColor);

        writer.Write(new Rune(codePoint));
        writer.Flush();

        Assert.Equal(expectedByteCount, stream.Length);
        Assert.Equal(expectedText, Utf8Text(stream));
    }

    [Fact]
    public void Write_MultipleRunes_AppendInOrder()
    {
        var stream = new MemoryStream();
        var writer = new AnsiTerminalWriter(stream, ColorSupport.TrueColor);

        writer.Write(new Rune('H'));
        writer.Write(new Rune('i'));
        writer.Write(new Rune('Я'));
        writer.Flush();

        Assert.Equal("HiЯ", Utf8Text(stream));
    }

    [Fact]
    public void Write_GrowsBeyondInitialCapacity_WithoutLosingBytes()
    {
        var stream = new MemoryStream();
        var writer = new AnsiTerminalWriter(stream, ColorSupport.TrueColor);
        var expected = new StringBuilder();

        for (var i = 0; i < 3000; i++)
        {
            writer.Write(new Rune('Я'));
            expected.Append('Я');
        }
        writer.Flush();

        Assert.True(expected.Length * 2 > 4096);
        Assert.Equal(expected.ToString(), Utf8Text(stream));
    }

    [Fact]
    public void Clear_IsNotWrittenUntilFlush()
    {
        var stream = new MemoryStream();
        var writer = new AnsiTerminalWriter(stream, ColorSupport.TrueColor);

        writer.Clear();
        Assert.Equal(0, stream.Length);

        writer.Flush();
        Assert.Equal($"{Esc}[2J", Text(stream));
    }

    [Fact]
    public void ShowCursor_EmitsDecPrivateModeSet()
    {
        var stream = new MemoryStream();
        var writer = new AnsiTerminalWriter(stream, ColorSupport.TrueColor);

        writer.ShowCursor();
        Assert.Equal(0, stream.Length);

        writer.Flush();
        Assert.Equal($"{Esc}[?25h", Text(stream));
    }

    [Fact]
    public void HideCursor_EmitsDecPrivateModeReset()
    {
        var stream = new MemoryStream();
        var writer = new AnsiTerminalWriter(stream, ColorSupport.TrueColor);

        writer.HideCursor();
        Assert.Equal(0, stream.Length);

        writer.Flush();
        Assert.Equal($"{Esc}[?25l", Text(stream));
    }

    [Fact]
    public void EnterAlternateScreen_EmitsDecPrivateModeSet()
    {
        var stream = new MemoryStream();
        var writer = new AnsiTerminalWriter(stream, ColorSupport.TrueColor);

        writer.EnterAlternateScreen();
        Assert.Equal(0, stream.Length);

        writer.Flush();
        Assert.Equal($"{Esc}[?1049h", Text(stream));
    }

    [Fact]
    public void ExitAlternateScreen_EmitsDecPrivateModeReset()
    {
        var stream = new MemoryStream();
        var writer = new AnsiTerminalWriter(stream, ColorSupport.TrueColor);

        writer.ExitAlternateScreen();
        Assert.Equal(0, stream.Length);

        writer.Flush();
        Assert.Equal($"{Esc}[?1049l", Text(stream));
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

    [Fact]
    public void HotPath_WritingAFrameOfPrimitives_AllocatesNoManagedMemory()
    {
        var writer = new AnsiTerminalWriter(new DiscardingStream(), ColorSupport.TrueColor);
        var foreground = Color.Rgb(255, 0, 0);
        var background = Color.Named(4);
        var style = StyleFlags.Bold | StyleFlags.Underline;
        var rune = new Rune('A');

        // Warm up: JIT every method once and let the internal buffer grow to its steady-state size,
        // so neither shows up as an "allocation" once we start measuring.
        for (var i = 0; i < 10; i++)
        {
            writer.MoveCursor(1, 2);
            writer.SetStyle(foreground, background, style);
            writer.Write(rune);
            writer.ShowCursor();
            writer.HideCursor();
            writer.EnterAlternateScreen();
            writer.ExitAlternateScreen();
            writer.Clear();
            writer.Flush();
        }

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

        for (var i = 0; i < 1000; i++)
        {
            writer.MoveCursor(1, 2);
            writer.SetStyle(foreground, background, style);
            writer.Write(rune);
            writer.ShowCursor();
            writer.HideCursor();
            writer.EnterAlternateScreen();
            writer.ExitAlternateScreen();
            writer.Clear();
            writer.Flush();
        }

        var allocatedAfter = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(allocatedBefore, allocatedAfter);
    }
}
