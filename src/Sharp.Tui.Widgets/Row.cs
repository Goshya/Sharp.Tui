using Sharp.Tui.Layout;

namespace Sharp.Tui.Widgets;

// Lays children out left-to-right along the width; each gets the full height of `area`
// (docs/SPEC.md §2.4). Fixed/Percent/Fill build a child's (SizeMode, Widget) slot — sizing lives
// on the slot, not on the widget itself, so a plain widget never carries layout-specific state.
public sealed record Row : LinearContainer
{
    public Row(IReadOnlyList<(SizeMode Mode, Widget Child)> children) : base(children)
    {
    }

    public static (SizeMode Mode, Widget Child) Fixed(int cells, Widget child)
    {
        ArgumentNullException.ThrowIfNull(child);
        return (SizeMode.Fixed(cells), child);
    }

    public static (SizeMode Mode, Widget Child) Percent(double percent, Widget child)
    {
        ArgumentNullException.ThrowIfNull(child);
        return (SizeMode.Percent(percent), child);
    }

    public static (SizeMode Mode, Widget Child) Fill(int weight, Widget child)
    {
        ArgumentNullException.ThrowIfNull(child);
        return (SizeMode.Fill(weight), child);
    }

    protected override int Extent(Rect area) => area.Width;

    protected override Rect Slice(Rect area, int start, int length) =>
        new(area.X + start, area.Y, length, area.Height);

    // A Row's own natural size ignores every child's SizeMode (Fixed/Percent/Fill only matter to
    // Render's Solve pass) — it just asks each child how big it wants to be, sums those widths
    // (its own axis), and takes the tallest child's height (the cross axis).
    public override Size Measure(Constraints constraints)
    {
        var width = 0;
        var height = 0;

        foreach (var (_, child) in Children)
        {
            var size = child.Measure(constraints);
            width += size.Width;
            height = Math.Max(height, size.Height);
        }

        return new Size(
            Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
            Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight));
    }
}
