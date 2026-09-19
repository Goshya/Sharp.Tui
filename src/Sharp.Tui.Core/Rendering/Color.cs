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

    public Color Downgrade(ColorSupport support) => support switch
    {
        ColorSupport.TrueColor => this,
        ColorSupport.NoColor => Default,
        ColorSupport.Indexed256 => Kind == ColorKind.Rgb
            ? Indexed(ColorConversion.RgbToIndexed256(_r, _g, _b))
            : this,
        ColorSupport.Named16 => Kind switch
        {
            ColorKind.Rgb => Named(ColorConversion.RgbToNamed16(_r, _g, _b)),
            ColorKind.Indexed256 => Named(ColorConversion.Indexed256ToNamed16(_r)),
            _ => this,
        },
        _ => throw new ArgumentOutOfRangeException(nameof(support), support, null),
    };

    public bool Equals(Color other) => Kind == other.Kind && _r == other._r && _g == other._g && _b == other._b;
    public override bool Equals(object? obj) => obj is Color c && Equals(c);
    public override int GetHashCode() => HashCode.Combine(Kind, _r, _g, _b);
}