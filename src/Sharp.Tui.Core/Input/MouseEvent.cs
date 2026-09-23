namespace Sharp.Tui.Core.Input;

// X/Y are 0-based cell coordinates, same convention as Buffer's indexer and ITerminalWriter.MoveCursor.
public readonly record struct MouseEvent(int X, int Y, MouseButton Button, MouseAction Action, KeyModifiers Modifiers = KeyModifiers.None);
