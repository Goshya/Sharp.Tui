using Sharp.Tui.Layout;
using Sharp.Tui.Widgets;
using Buffer = Sharp.Tui.Core.Rendering.Buffer;

namespace Sharp.Tui.Tests.Widgets;

// A widget whose Measure always reports a fixed, caller-chosen Size regardless of Constraints —
// lets container Measure tests (Row/Column/Stack) control each child's "natural size" precisely,
// without depending on Text's own measuring rules or on RecordingWidget's Constraints-echoing one.
internal sealed record FixedMeasureWidget(Size Size) : Widget
{
    public override void Render(Buffer buffer, Rect area)
    {
    }

    public override Size Measure(Constraints constraints) => Size;
}
