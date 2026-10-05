using System.Text;
using Sharp.Tui.Core.Rendering;
using Sharp.Tui.Core.Terminal;

namespace Sharp.Tui.Tests.Terminal;

public class RawModeTests
{
    // Any call at all is a failure: on an unsupported OS the terminal must be left untouched —
    // no alternate screen, no hidden cursor, not even a flush.
    private sealed class UntouchableTerminalWriter : ITerminalWriter
    {
        public void EnterAlternateScreen() => throw new InvalidOperationException(nameof(EnterAlternateScreen));
        public void ExitAlternateScreen() => throw new InvalidOperationException(nameof(ExitAlternateScreen));
        public void ShowCursor() => throw new InvalidOperationException(nameof(ShowCursor));
        public void HideCursor() => throw new InvalidOperationException(nameof(HideCursor));
        public void Flush() => throw new InvalidOperationException(nameof(Flush));
        public void MoveCursor(int x, int y) => throw new InvalidOperationException(nameof(MoveCursor));
        public void SetStyle(Color foreground, Color background, StyleFlags style) => throw new InvalidOperationException(nameof(SetStyle));
        public void Write(Rune rune) => throw new InvalidOperationException(nameof(Write));
        public void Clear() => throw new InvalidOperationException(nameof(Clear));
    }

    [Fact]
    public void Enter_OnMacOS_ThrowsBeforeTouchingTheTerminal()
    {
        var ex = Assert.Throws<PlatformNotSupportedException>(() =>
            RawMode.Enter(new UntouchableTerminalWriter(), isWindows: false, isLinux: false, isMacOS: true));

        Assert.Contains("macOS", ex.Message);
    }

    [Fact]
    public void Enter_OnMacOS_MessageSaysWhatIsSupportedAndLinksTheTrackingIssue()
    {
        var ex = Assert.Throws<PlatformNotSupportedException>(() =>
            RawMode.CreateToggle(isWindows: false, isLinux: false, isMacOS: true));

        Assert.Contains("Windows and Linux", ex.Message);
        Assert.Contains(RawMode.MacOsTrackingIssue, ex.Message);
        Assert.DoesNotContain('\n', ex.Message); // one actionable line, not a wall of text
    }

    [Fact]
    public void Enter_OnAnotherUnsupportedOs_ThrowsBeforeTouchingTheTerminal()
    {
        var ex = Assert.Throws<PlatformNotSupportedException>(() =>
            RawMode.Enter(new UntouchableTerminalWriter(), isWindows: false, isLinux: false, isMacOS: false));

        Assert.Contains("Windows and Linux", ex.Message);
        Assert.DoesNotContain("macOS", ex.Message);
    }

    [Fact]
    public void Enter_NullWriter_Throws() =>
        Assert.Throws<ArgumentNullException>(() => RawMode.Enter(null!));
}
