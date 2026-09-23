using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Sharp.Tui.Core.Terminal;

// tcgetattr/tcsetattr-based raw mode for Linux only — see Termios.cs and UnixTermiosFlags.cs
// for why macOS isn't covered by this class (different struct layout and flag values that
// haven't been verified against real headers). RawMode.Enter throws PlatformNotSupportedException
// on any other platform rather than risk this one silently mis-marshaling on macOS.
[SupportedOSPlatform("linux")]
internal sealed partial class UnixRawModeToggle : IRawModeToggle
{
    private const int StdinFileNo = 0;
    private const int Tcsanow = 0; // apply changes immediately, don't wait for pending output

    private Termios _originalTermios;

    public void EnableRawMode()
    {
        if (tcgetattr(StdinFileNo, out _originalTermios) != 0)
            throw new InvalidOperationException($"Failed to read termios (errno {Marshal.GetLastPInvokeError()}).");

        var raw = _originalTermios;
        raw.c_lflag = UnixTermiosFlags.ToRawLocalFlags(_originalTermios.c_lflag);

        if (tcsetattr(StdinFileNo, Tcsanow, in raw) != 0)
            throw new InvalidOperationException($"Failed to set termios (errno {Marshal.GetLastPInvokeError()}).");
    }

    public void RestoreOriginalMode()
    {
        // Best-effort, same reasoning as WindowsRawModeToggle: this also runs on the
        // crash/Ctrl+C restore paths, where throwing would only make things worse.
        tcsetattr(StdinFileNo, Tcsanow, in _originalTermios);
    }

    [LibraryImport("libc", SetLastError = true)]
    private static partial int tcgetattr(int fd, out Termios termios);

    [LibraryImport("libc", SetLastError = true)]
    private static partial int tcsetattr(int fd, int optionalActions, in Termios termios);
}
