using System.Text;
using Sharp.Tui.Core.Rendering;
using Sharp.Tui.Core.Terminal;

namespace Sharp.Tui.Tests.Terminal;

public class RawModeScopeTests
{
    private abstract record Call;
    private sealed record EnterAlternateScreenCall : Call;
    private sealed record ExitAlternateScreenCall : Call;
    private sealed record ShowCursorCall : Call;
    private sealed record HideCursorCall : Call;
    private sealed record FlushCall : Call;

    private sealed class RecordingTerminalWriter : ITerminalWriter
    {
        public List<Call> Calls { get; } = [];

        public void EnterAlternateScreen() => Calls.Add(new EnterAlternateScreenCall());
        public void ExitAlternateScreen() => Calls.Add(new ExitAlternateScreenCall());
        public void ShowCursor() => Calls.Add(new ShowCursorCall());
        public void HideCursor() => Calls.Add(new HideCursorCall());
        public void Flush() => Calls.Add(new FlushCall());

        public void MoveCursor(int x, int y) => throw new NotSupportedException();
        public void SetStyle(Color foreground, Color background, StyleFlags style) => throw new NotSupportedException();
        public void Write(Rune rune) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
    }

    private sealed class RecordingRawModeToggle : IRawModeToggle
    {
        public int EnableCalls { get; private set; }
        public int RestoreCalls { get; private set; }

        public void EnableRawMode() => EnableCalls++;
        public void RestoreOriginalMode() => RestoreCalls++;
    }

    [Fact]
    public void Construction_EnablesRawMode_EntersAlternateScreen_HidesCursor_ThenFlushes()
    {
        var writer = new RecordingTerminalWriter();
        var toggle = new RecordingRawModeToggle();

        using var scope = new RawModeScope(writer, toggle);

        Assert.Equal(1, toggle.EnableCalls);
        Assert.Equal<Call>(
            [new EnterAlternateScreenCall(), new HideCursorCall(), new FlushCall()],
            writer.Calls);
    }

    [Fact]
    public void Dispose_ShowsCursor_ExitsAlternateScreen_FlushesThenRestoresRawMode()
    {
        var writer = new RecordingTerminalWriter();
        var toggle = new RecordingRawModeToggle();
        var scope = new RawModeScope(writer, toggle);
        writer.Calls.Clear(); // only care about what Dispose itself does from here

        scope.Dispose();

        Assert.Equal(1, toggle.RestoreCalls);
        Assert.Equal<Call>(
            [new ShowCursorCall(), new ExitAlternateScreenCall(), new FlushCall()],
            writer.Calls);
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_OnlyRestoresOnce()
    {
        var writer = new RecordingTerminalWriter();
        var toggle = new RecordingRawModeToggle();
        var scope = new RawModeScope(writer, toggle);

        scope.Dispose();
        scope.Dispose();
        scope.Dispose();

        Assert.Equal(1, toggle.RestoreCalls);
    }
}
