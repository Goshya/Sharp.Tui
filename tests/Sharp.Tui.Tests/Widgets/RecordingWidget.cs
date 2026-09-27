using Sharp.Tui.Layout;
using Sharp.Tui.Widgets;
using Buffer = Sharp.Tui.Core.Rendering.Buffer;

namespace Sharp.Tui.Tests.Widgets;

// A test double that draws nothing and just remembers the Rect it was rendered with, so layout
// can be asserted on without a real widget or any pixels on screen. Mutable state here is a
// deliberate exception for a test double — production widgets stay pure functions of their
// props (docs/SPEC.md §2.5).
internal sealed record RecordingWidget : Widget
{
    public Rect? LastRect { get; private set; }

    public override void Render(Buffer buffer, Rect area) => LastRect = area;
    public override Size Measure(Constraints constraints) => new(constraints.MaxWidth, constraints.MaxHeight);
}
