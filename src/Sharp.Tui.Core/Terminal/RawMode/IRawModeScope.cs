using System;

namespace Sharp.Tui.Core.Terminal;

// RAII handle for interactive terminal input: entering switches to raw mode + alternate screen
// buffer + hidden cursor, Dispose() restores all three — reliably, including on Ctrl+C or an
// unhandled exception, not just on a normal `using` exit.
public interface IRawModeScope : IDisposable
{
}
