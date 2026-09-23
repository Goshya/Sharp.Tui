using System.Text;

namespace Sharp.Tui.Core.Input;

// Char is only meaningful when Code == KeyCode.Char; every other KeyCode leaves it at default.
// Ctrl+letter combos (e.g. the raw byte 0x01 for Ctrl+A) are represented as Code = Char,
// Char = 'a', Modifiers = Ctrl — not as a separate control-character code — so app code matches
// on the letter the user actually pressed rather than a raw control byte.
public readonly record struct KeyEvent(KeyCode Code, Rune Char, KeyModifiers Modifiers = KeyModifiers.None)
{
    public static KeyEvent FromChar(Rune ch, KeyModifiers modifiers = KeyModifiers.None) =>
        new(KeyCode.Char, ch, modifiers);

    public static KeyEvent FromCode(KeyCode code, KeyModifiers modifiers = KeyModifiers.None) =>
        new(code, default, modifiers);
}
