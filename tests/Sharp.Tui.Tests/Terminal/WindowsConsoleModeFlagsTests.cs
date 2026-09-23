using Sharp.Tui.Core.Terminal;

namespace Sharp.Tui.Tests.Terminal;

public class WindowsConsoleModeFlagsTests
{
    [Fact]
    public void ToRawInputMode_ClearsLineInputAndEcho_SetsVirtualTerminalInput()
    {
        const uint unrelatedBits = 0b1000_0000_0000; // some other flag, must survive untouched
        var original = unrelatedBits | WindowsConsoleModeFlags.EnableLineInput | WindowsConsoleModeFlags.EnableEchoInput;

        var raw = WindowsConsoleModeFlags.ToRawInputMode(original);

        Assert.Equal(0u, raw & WindowsConsoleModeFlags.EnableLineInput);
        Assert.Equal(0u, raw & WindowsConsoleModeFlags.EnableEchoInput);
        Assert.Equal(WindowsConsoleModeFlags.EnableVirtualTerminalInput, raw & WindowsConsoleModeFlags.EnableVirtualTerminalInput);
        Assert.Equal(unrelatedBits, raw & unrelatedBits);
    }

    [Fact]
    public void ToRawInputMode_AlreadyMissingLineInputAndEcho_StillSetsVirtualTerminalInput()
    {
        var raw = WindowsConsoleModeFlags.ToRawInputMode(originalMode: 0);

        Assert.Equal(WindowsConsoleModeFlags.EnableVirtualTerminalInput, raw);
    }

    [Fact]
    public void ToRawOutputMode_SetsVirtualTerminalProcessing_PreservesOtherBits()
    {
        const uint unrelatedBits = 0b0001_0000_0000_0000;

        var raw = WindowsConsoleModeFlags.ToRawOutputMode(unrelatedBits);

        Assert.Equal(WindowsConsoleModeFlags.EnableVirtualTerminalProcessing, raw & WindowsConsoleModeFlags.EnableVirtualTerminalProcessing);
        Assert.Equal(unrelatedBits, raw & unrelatedBits);
    }

    [Fact]
    public void ToRawOutputMode_AlreadyHasVirtualTerminalProcessing_IsIdempotent()
    {
        var alreadyRaw = WindowsConsoleModeFlags.ToRawOutputMode(originalMode: 0);

        var raw = WindowsConsoleModeFlags.ToRawOutputMode(alreadyRaw);

        Assert.Equal(alreadyRaw, raw);
    }
}
