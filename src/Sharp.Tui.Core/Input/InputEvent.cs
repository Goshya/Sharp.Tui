using System;

namespace Sharp.Tui.Core.Input;

// Tagged union over the four event kinds the parser/reader loop can produce — same shape as
// Color (Kind + private fields + factories), chosen over a class hierarchy so a stream of these
// (parsed at typing/mouse-move speed) doesn't allocate one heap object per event.
public readonly struct InputEvent : IEquatable<InputEvent>
{
    public InputEventKind Kind { get; }

    private readonly KeyEvent _key;
    private readonly MouseEvent _mouse;
    private readonly ResizeEvent _resize;
    private readonly string? _paste;

    private InputEvent(InputEventKind kind, KeyEvent key, MouseEvent mouse, ResizeEvent resize, string? paste)
    {
        Kind = kind;
        _key = key;
        _mouse = mouse;
        _resize = resize;
        _paste = paste;
    }

    public static InputEvent Key(KeyEvent key) => new(InputEventKind.Key, key, default, default, null);
    public static InputEvent Mouse(MouseEvent mouse) => new(InputEventKind.Mouse, default, mouse, default, null);
    public static InputEvent Resize(ResizeEvent resize) => new(InputEventKind.Resize, default, default, resize, null);
    public static InputEvent Paste(string text) => new(InputEventKind.Paste, default, default, default, text);

    public KeyEvent AsKey => Kind == InputEventKind.Key ? _key : throw new InvalidOperationException($"This InputEvent is {Kind}, not Key.");
    public MouseEvent AsMouse => Kind == InputEventKind.Mouse ? _mouse : throw new InvalidOperationException($"This InputEvent is {Kind}, not Mouse.");
    public ResizeEvent AsResize => Kind == InputEventKind.Resize ? _resize : throw new InvalidOperationException($"This InputEvent is {Kind}, not Resize.");
    public string AsPaste => Kind == InputEventKind.Paste ? _paste! : throw new InvalidOperationException($"This InputEvent is {Kind}, not Paste.");

    public bool Equals(InputEvent other) => Kind == other.Kind && Kind switch
    {
        InputEventKind.Key => _key.Equals(other._key),
        InputEventKind.Mouse => _mouse.Equals(other._mouse),
        InputEventKind.Resize => _resize.Equals(other._resize),
        InputEventKind.Paste => _paste == other._paste,
        _ => throw new ArgumentOutOfRangeException(),
    };

    public override bool Equals(object? obj) => obj is InputEvent e && Equals(e);

    public override int GetHashCode() => Kind switch
    {
        InputEventKind.Key => HashCode.Combine(Kind, _key),
        InputEventKind.Mouse => HashCode.Combine(Kind, _mouse),
        InputEventKind.Resize => HashCode.Combine(Kind, _resize),
        InputEventKind.Paste => HashCode.Combine(Kind, _paste),
        _ => (int)Kind,
    };

    public override string ToString() => Kind switch
    {
        InputEventKind.Key => $"Key({_key})",
        InputEventKind.Mouse => $"Mouse({_mouse})",
        InputEventKind.Resize => $"Resize({_resize})",
        InputEventKind.Paste => $"Paste(\"{_paste}\")",
        _ => Kind.ToString(),
    };
}
