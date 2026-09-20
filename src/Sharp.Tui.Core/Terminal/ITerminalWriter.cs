using Sharp.Tui.Core.Rendering;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sharp.Tui.Core.Terminal;

public interface ITerminalWriter
{
    void MoveCursor(int x, int y);                       // 0-based снаружи
    void SetStyle(Color foreground, Color background, StyleFlags style);
    void Write(Rune rune);
    void Clear();
    void ShowCursor();
    void HideCursor();
    void EnterAlternateScreen();
    void ExitAlternateScreen();
    void Flush();
}
