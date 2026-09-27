using Sharp.Tui.Layout;

namespace Sharp.Tui.Widgets;

// Lays children out top-to-bottom along the height; each gets the full width of `area`
// (docs/SPEC.md §2.4). See Row for the sizing-mode/slot reasoning — identical here, just the
// other axis.
public sealed record Column : LinearContainer
{
    public Column(IReadOnlyList<(SizeMode Mode, Widget Child)> children) : base(children)
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

    protected override int Extent(Rect area) => area.Height;

    protected override Rect Slice(Rect area, int start, int length) =>
        new(area.X, area.Y + start, area.Width, length);
}
