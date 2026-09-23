using System.Text;
using Sharp.Tui.Core.Rendering;
using Sharp.Tui.Core.Terminal;
using Buffer = Sharp.Tui.Core.Rendering.Buffer;

namespace Sharp.Tui.Tests.Rendering;

public class DiffRendererTests
{
    private abstract record Call;
    private sealed record MoveCursorCall(int X, int Y) : Call;
    private sealed record SetStyleCall(Color Foreground, Color Background, StyleFlags Style) : Call;
    private sealed record WriteCall(Rune Rune) : Call;
    private sealed record FlushCall : Call;

    private sealed class RecordingTerminalWriter : ITerminalWriter
    {
        public List<Call> Calls { get; } = [];

        public void MoveCursor(int x, int y) => Calls.Add(new MoveCursorCall(x, y));
        public void SetStyle(Color foreground, Color background, StyleFlags style) =>
            Calls.Add(new SetStyleCall(foreground, background, style));
        public void Write(Rune rune) => Calls.Add(new WriteCall(rune));
        public void Flush() => Calls.Add(new FlushCall());

        public void Clear() => throw new NotSupportedException();
        public void ShowCursor() => throw new NotSupportedException();
        public void HideCursor() => throw new NotSupportedException();
        public void EnterAlternateScreen() => throw new NotSupportedException();
        public void ExitAlternateScreen() => throw new NotSupportedException();
    }

    // A write sink with no backing storage, so the allocation test below measures only what
    // DiffRenderer/AnsiTerminalWriter allocate, not whatever the destination stream might.
    // Mirrors AnsiTerminalWriterTests' DiscardingStream — kept local rather than shared, same
    // as this file's own RecordingTerminalWriter is not shared with that one.
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

    private static Cell Cell(char ch, Color? fg = null, Color? bg = null, StyleFlags style = StyleFlags.None) =>
        new(new Rune(ch), fg ?? Color.Default, bg ?? Color.Default, style);

    [Fact]
    public void Render_NullBuffer_Throws()
    {
        var renderer = new DiffRenderer();

        Assert.Throws<ArgumentNullException>(() => renderer.Render(null!, new RecordingTerminalWriter()));
    }

    [Fact]
    public void Render_NullWriter_Throws()
    {
        var renderer = new DiffRenderer();

        Assert.Throws<ArgumentNullException>(() => renderer.Render(new Buffer(1, 1), null!));
    }

    [Fact]
    public void Render_FirstFrame_PaintsEveryRowFullWidth()
    {
        var renderer = new DiffRenderer();
        var writer = new RecordingTerminalWriter();
        var back = new Buffer(width: 2, height: 2);
        back[0, 0] = Cell('A');
        back[1, 0] = Cell('B');

        renderer.Render(back, writer);

        Assert.Equal<Call>(
            [
                new MoveCursorCall(0, 0),
                new SetStyleCall(Color.Default, Color.Default, StyleFlags.None),
                new WriteCall(new Rune('A')),
                new WriteCall(new Rune('B')),
                new MoveCursorCall(0, 1),
                new WriteCall(new Rune(' ')),
                new WriteCall(new Rune(' ')),
                new FlushCall(),
            ],
            writer.Calls);
    }

    [Fact]
    public void Render_NoChangesSinceLastFrame_EmitsOnlyFlush()
    {
        var renderer = new DiffRenderer();
        var first = new Buffer(width: 3, height: 1);
        first[1, 0] = Cell('X');
        renderer.Render(first, new RecordingTerminalWriter());

        var writer = new RecordingTerminalWriter();
        var second = new Buffer(width: 3, height: 1);
        second[1, 0] = Cell('X');
        renderer.Render(second, writer);

        Assert.Equal<Call>([new FlushCall()], writer.Calls);
    }

    [Fact]
    public void Render_SingleChangedCell_OnlyRepaintsThatCell()
    {
        var renderer = new DiffRenderer();
        renderer.Render(new Buffer(width: 4, height: 1), new RecordingTerminalWriter());

        var writer = new RecordingTerminalWriter();
        var back = new Buffer(width: 4, height: 1);
        back[2, 0] = Cell('Z');
        renderer.Render(back, writer);

        Assert.Equal<Call>(
            [
                new MoveCursorCall(2, 0),
                new SetStyleCall(Color.Default, Color.Default, StyleFlags.None),
                new WriteCall(new Rune('Z')),
                new FlushCall(),
            ],
            writer.Calls);
    }

    [Fact]
    public void Render_RunWithUniformStyle_EmitsSetStyleOnlyOnce()
    {
        var renderer = new DiffRenderer();
        renderer.Render(new Buffer(width: 3, height: 1), new RecordingTerminalWriter());

        var writer = new RecordingTerminalWriter();
        var red = Color.Rgb(255, 0, 0);
        var back = new Buffer(width: 3, height: 1);
        back[0, 0] = Cell('A', fg: red);
        back[1, 0] = Cell('B', fg: red);
        back[2, 0] = Cell('C', fg: red);
        renderer.Render(back, writer);

        Assert.Equal<Call>(
            [
                new MoveCursorCall(0, 0),
                new SetStyleCall(red, Color.Default, StyleFlags.None),
                new WriteCall(new Rune('A')),
                new WriteCall(new Rune('B')),
                new WriteCall(new Rune('C')),
                new FlushCall(),
            ],
            writer.Calls);
    }

    [Fact]
    public void Render_StyleChangeMidRun_EmitsSetStyleAgain()
    {
        var renderer = new DiffRenderer();
        renderer.Render(new Buffer(width: 3, height: 1), new RecordingTerminalWriter());

        var writer = new RecordingTerminalWriter();
        var red = Color.Rgb(255, 0, 0);
        var blue = Color.Rgb(0, 0, 255);
        var back = new Buffer(width: 3, height: 1);
        back[0, 0] = Cell('A', fg: red);
        back[1, 0] = Cell('B', fg: blue);
        back[2, 0] = Cell('C', fg: red);
        renderer.Render(back, writer);

        // The third cell reverts to red, but the comparison is against the *last emitted*
        // style (blue, from cell B) rather than _front, so SetStyle fires a third time —
        // not deduplicated against the earlier red from cell A.
        Assert.Equal<Call>(
            [
                new MoveCursorCall(0, 0),
                new SetStyleCall(red, Color.Default, StyleFlags.None),
                new WriteCall(new Rune('A')),
                new SetStyleCall(blue, Color.Default, StyleFlags.None),
                new WriteCall(new Rune('B')),
                new SetStyleCall(red, Color.Default, StyleFlags.None),
                new WriteCall(new Rune('C')),
                new FlushCall(),
            ],
            writer.Calls);
    }

    [Fact]
    public void Render_SameStyleAcrossTwoRunsInOneFrame_DoesNotReemitSetStyle()
    {
        // Two separate runs (far enough apart not to be merged by FrameDiff) sharing the same
        // style: SGR state is tracked for the whole frame, not reset per run, so the second
        // run's first cell must not pay for a redundant SetStyle.
        var renderer = new DiffRenderer();
        renderer.Render(new Buffer(width: 10, height: 1), new RecordingTerminalWriter());

        var writer = new RecordingTerminalWriter();
        var red = Color.Rgb(255, 0, 0);
        var back = new Buffer(width: 10, height: 1);
        back[0, 0] = Cell('A', fg: red);
        back[9, 0] = Cell('B', fg: red);
        renderer.Render(back, writer);

        Assert.Equal<Call>(
            [
                new MoveCursorCall(0, 0),
                new SetStyleCall(red, Color.Default, StyleFlags.None),
                new WriteCall(new Rune('A')),
                new MoveCursorCall(9, 0),
                new WriteCall(new Rune('B')),
                new FlushCall(),
            ],
            writer.Calls);
    }

    [Fact]
    public void Invalidate_ForcesFullRepaintOnNextRenderEvenWithoutChanges()
    {
        var renderer = new DiffRenderer();
        var back = new Buffer(width: 2, height: 1);
        back[0, 0] = Cell('A');
        renderer.Render(back, new RecordingTerminalWriter());

        renderer.Invalidate();

        var writer = new RecordingTerminalWriter();
        renderer.Render(back, writer);

        Assert.Equal<Call>(
            [
                new MoveCursorCall(0, 0),
                new SetStyleCall(Color.Default, Color.Default, StyleFlags.None),
                new WriteCall(new Rune('A')),
                new WriteCall(new Rune(' ')),
                new FlushCall(),
            ],
            writer.Calls);
    }

    [Fact]
    public void Render_AlwaysFlushesExactlyOnce_EvenWhenNothingChanged()
    {
        var renderer = new DiffRenderer();
        var back = new Buffer(width: 2, height: 2);
        renderer.Render(back, new RecordingTerminalWriter());

        var writer = new RecordingTerminalWriter();
        renderer.Render(back, writer);

        Assert.Single(writer.Calls.OfType<FlushCall>());
    }

    [Fact]
    public void Render_HotPath_AllocatesNoManagedMemoryAfterWarmup()
    {
        var renderer = new DiffRenderer();
        var writer = new AnsiTerminalWriter(new DiscardingStream(), ColorSupport.TrueColor);

        var a = new Buffer(width: 20, height: 10);
        var b = new Buffer(width: 20, height: 10);
        b[5, 3] = Cell('X', fg: Color.Rgb(255, 0, 0));
        b[15, 7] = Cell('Y', fg: Color.Rgb(0, 255, 0));

        // Toggle between two known buffers so every call does real diff work (not a settled,
        // permanently-empty diff) — that's what actually exercises FrameDiff's reused list.
        for (var i = 0; i < 10; i++)
            renderer.Render(i % 2 == 0 ? a : b, writer);

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

        for (var i = 0; i < 1000; i++)
            renderer.Render(i % 2 == 0 ? a : b, writer);

        var allocatedAfter = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(allocatedBefore, allocatedAfter);
    }
}
