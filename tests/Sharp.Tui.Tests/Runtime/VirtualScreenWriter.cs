using System.Text;
using Sharp.Tui.Core.Rendering;
using Sharp.Tui.Core.Terminal;

namespace Sharp.Tui.Tests.Runtime;

// A tiny fake terminal: applies MoveCursor/Write to a character grid so runtime tests can assert
// on what's actually on screen rather than on the exact sequence of writer calls. Styles, cursor
// visibility and the alternate screen are irrelevant here and ignored. Locked because the loop
// writes from its own continuation while the test thread polls for the expected text.
internal sealed class VirtualScreenWriter : ITerminalWriter
{
    private readonly object _lock = new();
    private readonly char[,] _cells;
    private int _x;
    private int _y;
    private int _flushCount;

    public VirtualScreenWriter(int width, int height)
    {
        _cells = new char[height, width];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
            _cells[y, x] = ' ';
    }

    public int FlushCount => Volatile.Read(ref _flushCount);

    public string Row(int y)
    {
        lock (_lock)
        {
            var width = _cells.GetLength(1);
            var chars = new char[width];
            for (var x = 0; x < width; x++)
                chars[x] = _cells[y, x];

            return new string(chars).TrimEnd();
        }
    }

    public void MoveCursor(int x, int y)
    {
        lock (_lock)
        {
            _x = x;
            _y = y;
        }
    }

    public void Write(Rune rune)
    {
        lock (_lock)
        {
            if (_y < _cells.GetLength(0) && _x < _cells.GetLength(1))
                _cells[_y, _x] = (char)rune.Value;

            _x++;
        }
    }

    public void Flush() => Interlocked.Increment(ref _flushCount);

    public void SetStyle(Color foreground, Color background, StyleFlags style) { }
    public void Clear() { }
    public void ShowCursor() { }
    public void HideCursor() { }
    public void EnterAlternateScreen() { }
    public void ExitAlternateScreen() { }
}
