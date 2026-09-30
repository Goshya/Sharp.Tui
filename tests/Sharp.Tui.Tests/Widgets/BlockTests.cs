using Sharp.Tui.Core.Rendering;
using Sharp.Tui.Layout;
using Sharp.Tui.Widgets;
using Buffer = Sharp.Tui.Core.Rendering.Buffer;

namespace Sharp.Tui.Tests.Widgets;

public class BlockTests
{
    // --- Constructor ---------------------------------------------------------------------------

    [Fact]
    public void Constructor_NullChild_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new Block(null!));
    }

    // --- Measure -----------------------------------------------------------------------------

    [Fact]
    public void Measure_AddsTwoToEachAxisOverChildsMeasure()
    {
        var block = new Block(new FixedMeasureWidget(new Size(5, 2)));

        Assert.Equal(new Size(7, 4), block.Measure(Constraints.Loose(new Size(100, 100))));
    }

    [Fact]
    public void Measure_ClampsToConstraints()
    {
        var block = new Block(new FixedMeasureWidget(new Size(50, 50)));

        Assert.Equal(new Size(10, 10), block.Measure(new Constraints(0, 10, 0, 10)));
    }

    [Fact]
    public void Measure_ChildSeesConstraintsShrunkByTheBorderNotTheOriginal()
    {
        // With an outer MaxWidth of 10, the child should be asked with MaxWidth 8 (10 - 2), not
        // 10 — RecordingConstraintsWidget below reports back whatever Constraints it was given.
        var child = new RecordingConstraintsWidget();
        var block = new Block(child);

        block.Measure(new Constraints(0, 10, 0, 10));

        Assert.Equal(new Constraints(0, 8, 0, 8), child.LastConstraints);
    }

    [Fact]
    public void Measure_NeverPassesNegativeConstraintsToChild_WhenOuterBudgetIsSmallerThanTheBorder()
    {
        var child = new RecordingConstraintsWidget();
        var block = new Block(child);

        block.Measure(new Constraints(0, 1, 0, 1));

        Assert.Equal(new Constraints(0, 0, 0, 0), child.LastConstraints);
    }

    // --- Style -----------------------------------------------------------------------------------

    [Fact]
    public void Render_NoStyle_BorderAndTitleAreUnstyled()
    {
        var buffer = new Buffer(10, 4);

        new Block(new Text("x"), title: "T").Render(buffer, new Rect(0, 0, 10, 4));

        var corner = buffer[0, 0];
        var title = buffer[2, 0]; // 'T' inside " T "
        Assert.Equal(Color.Default, corner.Foreground);
        Assert.Equal(StyleFlags.None, corner.Style);
        Assert.Equal(Color.Default, title.Foreground);
        Assert.Equal(StyleFlags.None, title.Style);
    }

    [Fact]
    public void Render_BorderStyle_AppliesToBorderCellsOnly()
    {
        var buffer = new Buffer(10, 4);
        var borderStyle = new Style(Foreground: Color.Named(2));

        new Block(new Text("x"), borderStyle: borderStyle).Render(buffer, new Rect(0, 0, 10, 4));

        Assert.Equal(Color.Named(2), buffer[0, 0].Foreground); // top-left corner
        Assert.Equal(Color.Named(2), buffer[9, 3].Foreground); // bottom-right corner
        Assert.Equal(Color.Named(2), buffer[5, 0].Foreground); // top edge fill
        Assert.Equal(Color.Default, buffer[1, 1].Foreground); // interior, untouched by border style
    }

    [Fact]
    public void Render_TitleStyle_AppliesToTitleCellsOnly_IndependentOfBorderStyle()
    {
        var buffer = new Buffer(10, 4);
        var borderStyle = new Style(Foreground: Color.Named(2));
        var titleStyle = new Style(Bold: true);

        new Block(new Text("x"), title: "Hi", borderStyle: borderStyle, titleStyle: titleStyle)
            .Render(buffer, new Rect(0, 0, 10, 4));

        // "┌ Hi ─..." — 'H' lands at x = 2.
        var titleCell = buffer[2, 0];
        Assert.Equal(StyleFlags.Bold, titleCell.Style);
        Assert.Equal(Color.Default, titleCell.Foreground); // TitleStyle didn't set one — its own default, not BorderStyle's

        var corner = buffer[0, 0];
        Assert.Equal(StyleFlags.None, corner.Style); // BorderStyle didn't set Bold
        Assert.Equal(Color.Named(2), corner.Foreground);
    }
}

// A test double whose Measure ignores its input and just remembers the last Constraints it was
// asked with — for asserting exactly what Block hands down to its child, independent of what the
// child would actually report.
internal sealed record RecordingConstraintsWidget : Widget
{
    public Constraints? LastConstraints { get; private set; }

    public override void Render(Buffer buffer, Rect area)
    {
    }

    public override Size Measure(Constraints constraints)
    {
        LastConstraints = constraints;
        return new Size(0, 0);
    }
}
