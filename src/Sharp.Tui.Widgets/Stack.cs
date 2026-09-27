using Sharp.Tui.Layout;
using Buffer = Sharp.Tui.Core.Rendering.Buffer;

namespace Sharp.Tui.Widgets;

// Z-ordered overlay container (docs/SPEC.md §2.4): every child gets the Stack's own Rect
// unchanged — no axis to solve, unlike Row/Column — and is drawn in list order, so later entries
// land on top of earlier ones in the buffer. The future basis for popups/modals/toasts (M4+).
public sealed record Stack : Widget
{
    public IReadOnlyList<Widget> Children { get; }

    public Stack(IReadOnlyList<Widget> children)
    {
        ArgumentNullException.ThrowIfNull(children);
        Children = children;
    }

    public override void Render(Buffer buffer, Rect area)
    {
        foreach (var child in Children)
            child.Render(buffer, area);
        
    }

    // Unlike Row/Column there's no axis to sum along — every child overlaps every other — so a
    // Stack wants to be exactly as big as its most demanding child, on each axis independently.
    public override Size Measure(Constraints constraints)
    {
        var width = 0;
        var height = 0;

        foreach (var child in Children)
        {
            var size = child.Measure(constraints);
            width = Math.Max(width, size.Width);
            height = Math.Max(height, size.Height);
        }

        return new Size(
            Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
            Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight));
    }
}
