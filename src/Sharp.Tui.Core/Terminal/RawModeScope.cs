using System;

namespace Sharp.Tui.Core.Terminal;

// The platform-agnostic half of IRawModeScope: hook registration, idempotent restore, and the
// alternate-screen/cursor calls that are already just ITerminalWriter methods from M1.2. The
// only thing it delegates out is the actual OS-level flag flip (IRawModeToggle).
internal sealed class RawModeScope : IRawModeScope
{
    private readonly ITerminalWriter _writer;
    private readonly IRawModeToggle _toggle;
    private bool _restored;

    public RawModeScope(ITerminalWriter writer, IRawModeToggle toggle)
    {
        _writer = writer;
        _toggle = toggle;

        _toggle.EnableRawMode();
        _writer.EnterAlternateScreen();
        _writer.HideCursor();
        _writer.Flush();

        // Belt and suspenders: Dispose() covers the normal `using` exit, these two cover
        // Ctrl+C and any termination path (including an unhandled exception) that skips it.
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        Console.CancelKeyPress += OnCancelKeyPress;
    }

    public void Dispose()
    {
        Restore();
        GC.SuppressFinalize(this);
    }

    private void OnProcessExit(object? sender, EventArgs e) => Restore();

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e) => Restore();

    // More than one of Dispose/ProcessExit/CancelKeyPress can fire for the same exit, so this
    // has to be safe to call twice — the guard makes every call after the first a no-op.
    private void Restore()
    {
        if (_restored)
            return;

        _restored = true;

        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        Console.CancelKeyPress -= OnCancelKeyPress;

        _writer.ShowCursor();
        _writer.ExitAlternateScreen();
        _writer.Flush();
        _toggle.RestoreOriginalMode();
    }
}
