using System;
using System.Collections.Generic;
using System.Text;

namespace Sharp.Tui.Core.Rendering;

public readonly struct Color : IEquatable<Color>
{
    public ColorKind Kind { get; }
    private readonly byte _r, _g, _b;

    public static readonly Color Default = default;

    public static Color Named(byte index0To15) => new(ColorKind.Named16, index0To15, 0, 0);
    public static Color Indexed(byte index0To255) => new(ColorKind.Indexed256, index0To255, 0, 0);
    public static Color Rgb(byte r, byte g, byte b) => new(ColorKind.Rgb, r, g, b);

    private Color(ColorKind kind, byte r, byte g, byte b) { Kind = kind; _r = r; _g = g; _b = b; }

    public bool Equals(Color other) => Kind == other.Kind && _r == other._r && _g == other._g && _b == other._b;
    public override bool Equals(object? obj) => obj is Color c && Equals(c);
    public override int GetHashCode() => HashCode.Combine(Kind, _r, _g, _b);
}