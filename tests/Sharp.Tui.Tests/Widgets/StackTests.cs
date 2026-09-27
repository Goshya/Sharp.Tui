using Sharp.Tui.Layout;
using Sharp.Tui.Widgets;
using Buffer = Sharp.Tui.Core.Rendering.Buffer;

namespace Sharp.Tui.Tests.Widgets;

public class StackTests
{
    private static readonly Buffer DummyBuffer = new(1, 1); // never touched — RecordingWidget ignores it

    [Fact]
    public void Render_EveryChildGetsTheStacksOwnRect()
    {
        var a = new RecordingWidget();
        var b = new RecordingWidget();
        var stack = new Stack([a, b]);

        var area = new Rect(2, 3, 10, 5);
        stack.Render(DummyBuffer, area);

        Assert.Equal(area, a.LastRect);
        Assert.Equal(area, b.LastRect);
    }

    [Fact]
    public void Render_LaterChildrenAreDrawnOnTopOfEarlierOnes()
    {
        // Real Text widgets, not RecordingWidget: this test needs actual buffer writes to prove
        // draw order, not just which Rect each child received.
        var buffer = new Buffer(1, 1);
        var stack = new Stack([new Text("A"), new Text("B")]);

        stack.Render(buffer, new Rect(0, 0, 1, 1));

        Assert.Equal('B', (char)buffer[0, 0].Char.Value);
    }

    [Fact]
    public void Render_EmptyStack_DoesNotThrow()
    {
        var stack = new Stack([]);

        stack.Render(DummyBuffer, new Rect(0, 0, 5, 5));
    }

    [Fact]
    public void Stack_ComposesInsideARowCell()
    {
        var a = new RecordingWidget();
        var b = new RecordingWidget();
        var stack = new Stack([a, b]);
        var sibling = new RecordingWidget();
        var row = new Row([Row.Fixed(10, stack), Row.Fill(1, sibling)]);

        row.Render(DummyBuffer, new Rect(0, 0, 30, 5));

        Assert.Equal(new Rect(0, 0, 10, 5), a.LastRect);
        Assert.Equal(new Rect(0, 0, 10, 5), b.LastRect);
        Assert.Equal(new Rect(10, 0, 20, 5), sibling.LastRect);
    }

    [Fact]
    public void Constructor_NullChildren_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new Stack(null!));
    }

    // --- Measure -----------------------------------------------------------------------------
    // A Stack wants to be exactly as big as its most demanding child, on each axis independently
    // — unlike Row/Column, there's no axis to sum along, every child overlaps every other.

    private static readonly Constraints Loose = Constraints.Loose(new Size(100, 100));

    [Fact]
    public void Measure_TakesMaxOfEachAxisAcrossChildren()
    {
        var a = new FixedMeasureWidget(new Size(10, 2));
        var b = new FixedMeasureWidget(new Size(3, 8));
        var stack = new Stack([a, b]);

        Assert.Equal(new Size(10, 8), stack.Measure(Loose));
    }

    [Fact]
    public void Measure_NoChildren_IsZero()
    {
        var stack = new Stack([]);

        Assert.Equal(new Size(0, 0), stack.Measure(Loose));
    }

    [Fact]
    public void Measure_ClampsToConstraints()
    {
        var a = new FixedMeasureWidget(new Size(50, 50));
        var stack = new Stack([a]);

        Assert.Equal(new Size(10, 10), stack.Measure(new Constraints(0, 10, 0, 10)));
    }
}
