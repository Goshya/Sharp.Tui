using Sharp.Tui.Layout;
using Buffer = Sharp.Tui.Core.Rendering.Buffer;

namespace Sharp.Tui.Widgets;

// Shared Render logic for Row and Column (docs/SPEC.md §2.4): the two differ only in which axis
// is "their own" (solved via LayoutSolver) versus "the cross axis" (the child just gets the full
// size of). Extent/Slice are the only two things that differ. Public — a public sealed type can't
// derive from an internal base (CS0060) — but there's no SizeMode-style reason to close this one
// off: nothing pattern-matches on the concrete container type, so a third-party linear container
// built on this base is a reasonable, unproblematic extension point, not a bug waiting to happen.
public abstract record LinearContainer : Widget
{
    protected LinearContainer(IReadOnlyList<(SizeMode Mode, Widget Child)> children)
    {
        ArgumentNullException.ThrowIfNull(children);
        Children = children;
    }

    public IReadOnlyList<(SizeMode Mode, Widget Child)> Children { get; }

    // The size of `area` along this container's own axis: Width for Row, Height for Column.
    protected abstract int Extent(Rect area);

    // A child's Rect: offset by `start` and `length` long on this container's own axis,
    // spanning `area`'s full size on the cross axis.
    protected abstract Rect Slice(Rect area, int start, int length);

    // Projects a measured Size onto this container's own axis — the other half of Extent, needed
    // to turn an Auto child's Measure result into a single number the solver can use.
    protected abstract int ExtentOf(Size size);

    public override void Render(Buffer buffer, Rect area)
    {
        var modes = new SizeMode[Children.Count];
        for (var i = 0; i < Children.Count; i++)
            modes[i] = Children[i].Mode;

        // Asked with the container's own full area, loosely — an Auto child's Measure result is
        // its own intrinsic size, independent of what any Fixed/Auto sibling already claimed;
        // Solve's own remaining-budget clamp (same one Fixed already goes through) reins it in
        // if it doesn't actually fit.
        var measureConstraints = Constraints.Loose(new Size(area.Width, area.Height));
        int MeasureAuto(int i) => ExtentOf(Children[i].Child.Measure(measureConstraints));

        var sizes = LayoutSolver.Solve(Extent(area), modes, MeasureAuto);

        var offset = 0;
        for (var i = 0; i < Children.Count; i++)
        {
            // Intersect defensively: LayoutSolver's own invariants already keep this within
            // `area`, but a child should never be handed a Rect that overflows its parent even
            // if that stops being true for some future sizing mode.
            var childArea = Slice(area, offset, sizes[i]).Intersect(area);
            Children[i].Child.Render(buffer, childArea);
            offset += sizes[i];
        }
    }
}
