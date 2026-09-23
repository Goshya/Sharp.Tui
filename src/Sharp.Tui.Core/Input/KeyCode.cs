namespace Sharp.Tui.Core.Input;

// `Char` carries no data of its own — the actual character is KeyEvent.Char, meaningful only
// when Code == Char. Everything else here is a key with no natural character representation.
public enum KeyCode : byte
{
    Char,
    Enter,
    Tab,
    Backspace,
    Escape,
    Delete,
    Insert,
    Up,
    Down,
    Left,
    Right,
    Home,
    End,
    PageUp,
    PageDown,
    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
}
