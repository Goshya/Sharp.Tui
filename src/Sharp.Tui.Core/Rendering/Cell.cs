using System;
using System.Collections.Generic;
using System.Text;

namespace Sharp.Tui.Core.Rendering;

public readonly struct Cell : IEquatable<Cell>
{
    public readonly Rune Char;
    public readonly Color Foreground;
    public readonly Color Background;
    public readonly StyleFlags Style;

    public Cell(Rune ch, Color fg, Color bg, StyleFlags style)
    {
        Char = ch; Foreground = fg; Background = bg; Style = style;
    }

    public static readonly Cell Blank = new(new Rune(' '), Color.Default, Color.Default, StyleFlags.None);

    public bool Equals(Cell other) =>
        Char == other.Char && Foreground.Equals(other.Foreground) &&
        Background.Equals(other.Background) && Style == other.Style;

    public override bool Equals(object? obj) => obj is Cell c && Equals(c);
    public override int GetHashCode() => HashCode.Combine(Char, Foreground, Background, Style);
}
