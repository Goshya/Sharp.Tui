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
}
