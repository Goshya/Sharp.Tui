namespace Sharp.Tui.Core.Input;

// None covers pure Move (no button held) and scroll actions, neither of which has a button.
public enum MouseButton : byte
{
    None,
    Left,
    Middle,
    Right,
}
