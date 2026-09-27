using Sharp.Tui.Layout;
using Buffer = Sharp.Tui.Core.Rendering.Buffer;

namespace Sharp.Tui.Widgets;

// Deliberately just Render for now: IApp.View needs a final-shaped return type from day one, so
// its signature doesn't have to change again when M3/M4 land, but Measure (which needs
// Constraints/Size) and the real widget set are M3/M4's job. Widgets are immutable records —
// all mutable state lives in the app's Model (docs/SPEC.md §2.5).
public abstract record Widget
{
    public abstract void Render(Buffer buffer, Rect area);
    public abstract Size Measure(Constraints constraints);
}
