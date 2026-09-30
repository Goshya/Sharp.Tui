using System.Text;
using Sharp.Tui.Core.Rendering;
using Sharp.Tui.Layout;
using Buffer = Sharp.Tui.Core.Rendering.Buffer;

namespace Sharp.Tui.Widgets;

// A simple percentage fill — no content to measure, no children, just a fraction and two styles
// (docs/SPEC.md §2.5). Filled cells draw a solid block ('█'), empty cells a space: the shape is
// legible by character alone, not just by color, so it still reads correctly even when the
// terminal downgrades to NoColor (ColorSupport, from M1) — color is a bonus layered on top via
// FilledStyle/EmptyStyle, never the only signal.
public sealed record Gauge : Widget
{
    private const char FilledChar = '█';
    private const char EmptyChar = ' ';

    public double Fraction { get; }
    public Style? FilledStyle { get; }
    public Style? EmptyStyle { get; }
    public string? Label { get; }

    public Gauge(double fraction, Style? filledStyle = null, Style? emptyStyle = null, string? label = null)
    {
        // A live value (CPU%, download progress) can legitimately overshoot momentarily —
        // clamping, not throwing, is the whole point of accepting a raw double here. NaN clamps
        // to 0 rather than passing through as-is: Math.Clamp leaves NaN untouched (comparisons
        // with NaN are always false), which would otherwise reach Math.Round as garbage below.
        Fraction = double.IsNaN(fraction) ? 0 : Math.Clamp(fraction, 0, 1);
        FilledStyle = filledStyle;
        EmptyStyle = emptyStyle;
        Label = label;
    }

    public override void Render(Buffer buffer, Rect area)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        // Same rounding convention LayoutSolver/Text already use, for consistency; Clamp is a
        // cheap defensive backstop against floating-point overshoot right at Fraction == 1.
        var filledWidth = Math.Clamp(
            (int)Math.Round(area.Width * Fraction, MidpointRounding.AwayFromZero), 0, area.Width);

        var (filledFg, filledBg, filledFlags) = (FilledStyle ?? Style.Default).Resolve();
        var (emptyFg, emptyBg, emptyFlags) = (EmptyStyle ?? Style.Default).Resolve();

        // Every row of `area` gets the same split, not just the first — the full-frame render
        // contract means a Gauge given more height than one row becomes a solid block, not a
        // filled top row over blank ones below.
        for (var y = area.Y; y < area.Y + area.Height; y++)
        {
            for (var i = 0; i < area.Width; i++)
            {
                var x = area.X + i;
                buffer[x, y] = i < filledWidth
                    ? new Cell(new Rune(FilledChar), filledFg, filledBg, filledFlags)
                    : new Cell(new Rune(EmptyChar), emptyFg, emptyBg, emptyFlags);
            }
        }

        if (string.IsNullOrEmpty(Label) || area.Width == 0 || area.Height == 0)
            return;

        // Centered both ways; truncated (not elided) if it doesn't fit, the same rule
        // Text/Block.Title already follow. Label has no style of its own (there's no LabelStyle
        // in the constructor) — each of its cells instead takes on whichever half of the bar it
        // lands on, overwriting the block/space character drawn above it.
        var runes = Label.EnumerateRunes().ToArray();
        var labelWidth = Math.Min(runes.Length, area.Width);
        var startX = area.X + (area.Width - labelWidth) / 2;
        var labelY = area.Y + area.Height / 2;

        for (var i = 0; i < labelWidth; i++)
        {
            var x = startX + i;
            var (fg, bg, flags) = (x - area.X) < filledWidth
                ? (filledFg, filledBg, filledFlags)
                : (emptyFg, emptyBg, emptyFlags);

            buffer[x, labelY] = new Cell(runes[i], fg, bg, flags);
        }
    }

    // No intrinsic content size — a Gauge is happy at any size it's given, so it asks for the
    // smallest it's allowed to be and leaves actual sizing entirely to the container (typically
    // Fill, since Auto has nothing here to measure).
    public override Size Measure(Constraints constraints) => new(constraints.MinWidth, constraints.MinHeight);
}
