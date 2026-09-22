using Sharp.Tui.Core.Rendering;

namespace Sharp.Tui.Tests.Rendering;

public class TerminalCapabilitiesTests
{
    private static TerminalCapabilities Detect(IReadOnlyDictionary<string, string> env, bool isOutputRedirected = false) =>
        TerminalCapabilities.Detect(key => env.GetValueOrDefault(key), isOutputRedirected);

    [Fact]
    public void NoEnvironmentInfoAtAll_FallsBackToPlatformDefault()
    {
        // No TERM/COLORTERM/WT_SESSION at all is ambiguous, not a signal either way. On Unix that's
        // treated the same as TERM=dumb — conservative, matching tools like Node's supports-color.
        // On Windows, TERM simply isn't part of the platform's convention (cmd.exe and PowerShell
        // never set it, with or without Windows Terminal), so the same absence there says nothing
        // about capability — assume basic 16-color support rather than degrading every native
        // Windows console to no color.
        var caps = Detect(new Dictionary<string, string>());

        var expected = OperatingSystem.IsWindows() ? ColorSupport.Named16 : ColorSupport.NoColor;
        Assert.Equal(expected, caps.ColorSupport);
    }

    [Fact]
    public void NoColorEnvVar_ForcesNoColor()
    {
        var caps = Detect(new Dictionary<string, string> { ["NO_COLOR"] = "1" });

        Assert.Equal(ColorSupport.NoColor, caps.ColorSupport);
    }

    [Fact]
    public void NoColorEnvVar_WinsOverColorTerm()
    {
        var caps = Detect(new Dictionary<string, string>
        {
            ["NO_COLOR"] = "1",
            ["COLORTERM"] = "truecolor",
        });

        Assert.Equal(ColorSupport.NoColor, caps.ColorSupport);
    }

    [Fact]
    public void OutputRedirected_ForcesNoColor_EvenWithColorTerm()
    {
        var caps = Detect(
            new Dictionary<string, string> { ["COLORTERM"] = "truecolor" },
            isOutputRedirected: true);

        Assert.Equal(ColorSupport.NoColor, caps.ColorSupport);
    }

    [Theory]
    [InlineData("truecolor")]
    [InlineData("24bit")]
    public void ColorTerm_ReportsTrueColor(string colorTermValue)
    {
        var caps = Detect(new Dictionary<string, string> { ["COLORTERM"] = colorTermValue });

        Assert.Equal(ColorSupport.TrueColor, caps.ColorSupport);
    }

    [Fact]
    public void WindowsTerminalSession_ReportsTrueColor()
    {
        var caps = Detect(new Dictionary<string, string> { ["WT_SESSION"] = "some-guid" });

        Assert.Equal(ColorSupport.TrueColor, caps.ColorSupport);
    }

    [Theory]
    [InlineData("xterm-256color")]
    [InlineData("screen-256color")]
    public void Term256Color_ReportsIndexed256(string termValue)
    {
        var caps = Detect(new Dictionary<string, string> { ["TERM"] = termValue });

        Assert.Equal(ColorSupport.Indexed256, caps.ColorSupport);
    }

    [Fact]
    public void PlainXterm_ReportsNamed16()
    {
        var caps = Detect(new Dictionary<string, string> { ["TERM"] = "xterm" });

        Assert.Equal(ColorSupport.Named16, caps.ColorSupport);
    }

    [Fact]
    public void ExplicitDumbTerm_ReportsNoColor_OnAnyPlatform()
    {
        // Unlike an absent TERM, "dumb" is an explicit signal (e.g. set by Emacs' shell-mode) —
        // it must win regardless of platform, so it gets its own test from the empty-TERM case below.
        var caps = Detect(new Dictionary<string, string> { ["TERM"] = "dumb" });

        Assert.Equal(ColorSupport.NoColor, caps.ColorSupport);
    }

    [Fact]
    public void EmptyTerm_FallsBackToPlatformDefault()
    {
        var caps = Detect(new Dictionary<string, string> { ["TERM"] = "" });

        var expected = OperatingSystem.IsWindows() ? ColorSupport.Named16 : ColorSupport.NoColor;
        Assert.Equal(expected, caps.ColorSupport);
    }

    [Fact]
    public void PublicDetect_DoesNotThrow_AndReturnsDefinedValue()
    {
        var caps = TerminalCapabilities.Detect();

        Assert.True(Enum.IsDefined(caps.ColorSupport));
    }
}
