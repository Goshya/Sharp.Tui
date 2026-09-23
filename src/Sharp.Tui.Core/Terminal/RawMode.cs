using System;

namespace Sharp.Tui.Core.Terminal;

public static class RawMode
{
    // macOS isn't implemented yet — its termios layout and flag values differ from Linux's and
    // haven't been verified against real headers, so this fails loudly rather than risk
    // mis-marshaling into the kernel. Fails loudly rather than silently no-op'ing everywhere
    // else too, so a missing platform shows up immediately instead of as "keys don't work" later.
    public static IRawModeScope Enter(ITerminalWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        IRawModeToggle toggle = OperatingSystem.IsWindows() ? new WindowsRawModeToggle()
            : OperatingSystem.IsLinux() ? new UnixRawModeToggle()
            : throw new PlatformNotSupportedException(
                "Raw mode is only implemented for Windows and Linux so far (see M1.4 for macOS status).");

        return new RawModeScope(writer, toggle);
    }
}
