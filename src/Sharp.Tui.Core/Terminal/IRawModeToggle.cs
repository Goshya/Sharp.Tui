namespace Sharp.Tui.Core.Terminal;

// The one thing that's genuinely platform-specific about raw mode: flipping the OS-level
// terminal flags (termios on Unix, console mode on Windows) and remembering how to flip them
// back. Everything else RawModeScope needs (alternate screen, cursor, crash-safety hooks) is
// already platform-agnostic via ITerminalWriter and .NET's own event model.
internal interface IRawModeToggle
{
    void EnableRawMode();
    void RestoreOriginalMode();
}
