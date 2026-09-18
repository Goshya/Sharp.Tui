using Sharp.Tui.Core.Rendering;

namespace Sharp.Tui.Tests.Rendering;

public class TerminalCapabilitiesTests
{
    private static TerminalCapabilities Detect(IReadOnlyDictionary<string, string> env, bool isOutputRedirected = false) =>
        TerminalCapabilities.Detect(key => env.GetValueOrDefault(key), isOutputRedirected);

    [Fact]
    public void NoEnvironmentInfoAtAll_IsConservativeAndReportsNoColor()
    {
        // Absent TERM (no signal at all) is treated the same as TERM=dumb — a deliberately
        // conservative default, matching the convention used by tools like Node's supports-color.
        var caps = Detect(new Dictionary<string, string>());

        Assert.Equal(ColorSupport.NoColor, caps.ColorSupport);
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

    [Theory]
    [InlineData("dumb")]
    [InlineData("")]
    public void DumbOrEmptyTerm_ReportsNoColor(string termValue)
    {
        var caps = Detect(new Dictionary<string, string> { ["TERM"] = termValue });

        Assert.Equal(ColorSupport.NoColor, caps.ColorSupport);
    }

    [Fact]
    public void PublicDetect_DoesNotThrow_AndReturnsDefinedValue()
    {
        var caps = TerminalCapabilities.Detect();

        Assert.True(Enum.IsDefined(caps.ColorSupport));
    }
}
