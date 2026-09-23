namespace Sharp.Tui.Core.Terminal;

// Pure bit-flag math for SetConsoleMode, kept separate from WindowsRawModeToggle's actual
// P/Invoke calls so it's testable without a real console attached.
internal static class WindowsConsoleModeFlags
{
    // Input mode flags (apply to the STD_INPUT_HANDLE mode).
    internal const uint EnableLineInput = 0x0002;
    internal const uint EnableEchoInput = 0x0004;
    internal const uint EnableVirtualTerminalInput = 0x0200;

    // Output mode flag (applies to the STD_OUTPUT_HANDLE mode) — numerically the same bit as
    // EnableEchoInput above, but it's a different flag entirely: each handle has its own flag
    // namespace, this just happens to reuse the same bit position.
    internal const uint EnableVirtualTerminalProcessing = 0x0004;

    internal static uint ToRawInputMode(uint originalMode) =>
        (originalMode & ~(EnableLineInput | EnableEchoInput)) | EnableVirtualTerminalInput;

    internal static uint ToRawOutputMode(uint originalMode) =>
        originalMode | EnableVirtualTerminalProcessing;
}
