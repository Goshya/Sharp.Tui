using System;
using System.Runtime.InteropServices;

namespace Sharp.Tui.Core.Terminal;

public static class RawMode
{
    internal const string MacOsTrackingIssue = "https://github.com/Goshya/Sharp.Tui/issues/35";

    // macOS isn't implemented yet — its termios layout and flag values differ from Linux's and
    // haven't been verified against real headers, so this fails loudly rather than risk
    // mis-marshaling into the kernel. Fails loudly rather than silently no-op'ing everywhere
    // else too, so a missing platform shows up immediately instead of as "keys don't work" later.
    //
    // The platform is decided before anything is written to `writer`: an unsupported OS throws
    // here, ahead of the alternate-screen / hide-cursor sequences RawModeScope emits, so the
    // user's terminal is left exactly as it was.
    public static IRawModeScope Enter(ITerminalWriter writer) =>
        Enter(writer, OperatingSystem.IsWindows(), OperatingSystem.IsLinux(), OperatingSystem.IsMacOS());

    // The platform flags are parameters (rather than OperatingSystem.IsX() inside) so the
    // unsupported-platform path can be tested on a machine that is Windows or Linux.
    internal static IRawModeScope Enter(ITerminalWriter writer, bool isWindows, bool isLinux, bool isMacOS)
    {
        ArgumentNullException.ThrowIfNull(writer);

        return new RawModeScope(writer, CreateToggle(isWindows, isLinux, isMacOS));
    }

    // CA1416 can't see through the bool parameters; the only production caller passes
    // OperatingSystem.IsWindows()/IsLinux() directly, so each toggle is built only on its own OS.
#pragma warning disable CA1416
    internal static IRawModeToggle CreateToggle(bool isWindows, bool isLinux, bool isMacOS)
    {
        if (isWindows)
            return new WindowsRawModeToggle();

        if (isLinux)
            return new UnixRawModeToggle();
#pragma warning restore CA1416

        throw new PlatformNotSupportedException(isMacOS
            ? "Sharp.Tui does not support macOS yet: raw terminal mode is implemented for Windows and Linux only. " +
              $"Progress and how to help: {MacOsTrackingIssue}"
            : $"Sharp.Tui does not support this operating system ({RuntimeInformation.OSDescription}): " +
              "raw terminal mode is implemented for Windows and Linux only.");
    }
}
