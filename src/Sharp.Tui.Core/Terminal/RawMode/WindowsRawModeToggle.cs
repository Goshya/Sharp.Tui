using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Sharp.Tui.Core.Terminal;

// SetConsoleMode-based raw mode for Windows. Touches both handles: input (line-input/echo off,
// so keys arrive byte-by-byte without the console printing them itself) and output (virtual
// terminal processing on, so AnsiTerminalWriter's raw ANSI bytes are actually interpreted
// instead of relying on whatever System.Console's own static init happens to enable).
[SupportedOSPlatform("windows")]
internal sealed partial class WindowsRawModeToggle : IRawModeToggle
{
    private const int StdInputHandle = -10;
    private const int StdOutputHandle = -11;

    private nint _inputHandle;
    private nint _outputHandle;
    private uint _originalInputMode;
    private uint _originalOutputMode;

    public void EnableRawMode()
    {
        _inputHandle = GetStdHandle(StdInputHandle);
        _outputHandle = GetStdHandle(StdOutputHandle);

        if (!GetConsoleMode(_inputHandle, out _originalInputMode))
            throw new InvalidOperationException($"Failed to read the console input mode (Win32 error {Marshal.GetLastWin32Error()}).");
        if (!GetConsoleMode(_outputHandle, out _originalOutputMode))
            throw new InvalidOperationException($"Failed to read the console output mode (Win32 error {Marshal.GetLastWin32Error()}).");

        if (!SetConsoleMode(_inputHandle, WindowsConsoleModeFlags.ToRawInputMode(_originalInputMode)))
            throw new InvalidOperationException($"Failed to set the console input mode (Win32 error {Marshal.GetLastWin32Error()}).");
        if (!SetConsoleMode(_outputHandle, WindowsConsoleModeFlags.ToRawOutputMode(_originalOutputMode)))
            throw new InvalidOperationException($"Failed to set the console output mode (Win32 error {Marshal.GetLastWin32Error()}).");
    }

    public void RestoreOriginalMode()
    {
        // Best-effort: this also runs on the crash/Ctrl+C restore paths, where throwing would
        // only make things worse (or mask whatever actually went wrong) — there's nothing more
        // useful to do with a failure here than leave the terminal as close to restored as
        // managed.
        SetConsoleMode(_inputHandle, _originalInputMode);
        SetConsoleMode(_outputHandle, _originalOutputMode);
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial nint GetStdHandle(int nStdHandle);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetConsoleMode(nint hConsoleHandle, out uint lpMode);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetConsoleMode(nint hConsoleHandle, uint dwMode);
}
