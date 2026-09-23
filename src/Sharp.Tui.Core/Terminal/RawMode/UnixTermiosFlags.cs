namespace Sharp.Tui.Core.Terminal;

// Pure bit-flag math for termios' c_lflag, kept separate from UnixRawModeToggle's actual
// P/Invoke calls so it's testable without a real terminal attached.
//
// Values are Linux (glibc) bits/termios.h — NOT portable to macOS/BSD, where ICANON has a
// different numeric value (0x100, not 0x2) and the termios struct layout itself differs
// (no c_line field, a shorter c_cc). UnixRawModeToggle is Linux-only for the same reason;
// see its own comment for why macOS isn't covered yet.
internal static class UnixTermiosFlags
{
    internal const uint Icanon = 0x00000002;
    internal const uint Echo = 0x00000008;

    internal static uint ToRawLocalFlags(uint originalLocalFlags) => originalLocalFlags & ~(Icanon | Echo);
}
