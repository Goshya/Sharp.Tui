using Sharp.Tui.Layout;
using Sharp.Tui.Widgets;
using Buffer = Sharp.Tui.Core.Rendering.Buffer;

namespace Sharp.Tui.Tests.Widgets;

public class GridTests
{
    private static readonly Buffer DummyBuffer = new(1, 1); // never touched — RecordingWidget ignores it

    // --- Render: the 2x2 "Done when" example from #15 --------------------------------------------

    [Fact]
    public void Render_MixedTracks_AssignsCorrectRectPerCell()
    {
        // Columns: Fixed(20), Fill(1). Rows: Auto (sized to the tallest child in it), Fill(1).
        // `sidebar` shares the Auto row with `header`, so it must be a widget with a controlled
        // Measure result too (shorter than header's) — RecordingWidget's own Measure echoes back
        // whatever Constraints it's given (the row's full height), which would otherwise win the
        // "tallest child" comparison and defeat the point of this test.
        var header = new FixedMeasureWidget(new Size(5, 2));
        var sidebar = new FixedMeasureWidget(new Size(3, 1));
        var body = new RecordingWidget();
        var footer = new RecordingWidget();

        var grid = new Grid(
            rows: [SizeMode.Auto(), SizeMode.Fill(1)],
            columns: [SizeMode.Fixed(20), SizeMode.Fill(1)],
            children: [
                Grid.At(0, 0, header),
                Grid.At(0, 1, sidebar),
                Grid.At(1, 0, body),
                Grid.At(1, 1, footer),
            ]);

        grid.Render(DummyBuffer, new Rect(0, 0, 50, 10));

        // Row 0 height = header's measured height (2). Row 1 = remaining 8.
        // Column 0 width = 20 (Fixed). Column 1 = remaining 30.
        Assert.Equal(new Rect(0, 0, 20, 2), header.LastRect);
        Assert.Equal(new Rect(20, 0, 30, 2), sidebar.LastRect);
        Assert.Equal(new Rect(0, 2, 20, 8), body.LastRect);
        Assert.Equal(new Rect(20, 2, 30, 8), footer.LastRect);
    }

    // --- Auto tracks with more than one child in them ---------------------------------------------

    [Fact]
    public void Render_AutoRowWithMultipleChildren_SizesToTallestChild()
    {
        var a = new FixedMeasureWidget(new Size(1, 2));
        var b = new FixedMeasureWidget(new Size(1, 5));
        var below = new RecordingWidget();

        var grid = new Grid(
            rows: [SizeMode.Auto(), SizeMode.Fill(1)],
            columns: [SizeMode.Fixed(1), SizeMode.Fixed(1)],
            children: [Grid.At(0, 0, a), Grid.At(0, 1, b), Grid.At(1, 0, below)]);

        grid.Render(DummyBuffer, new Rect(0, 0, 2, 20));

        Assert.Equal(new Rect(0, 5, 1, 15), below.LastRect);
    }

    [Fact]
    public void Render_AutoColumnWithMultipleChildren_SizesToWidestChild()
    {
        var a = new FixedMeasureWidget(new Size(3, 1));
        var b = new FixedMeasureWidget(new Size(7, 1));
        var right = new RecordingWidget();

        var grid = new Grid(
            rows: [SizeMode.Fixed(1), SizeMode.Fixed(1)],
            columns: [SizeMode.Auto(), SizeMode.Fill(1)],
            children: [Grid.At(0, 0, a), Grid.At(1, 0, b), Grid.At(0, 1, right)]);

        grid.Render(DummyBuffer, new Rect(0, 0, 20, 2));

        Assert.Equal(new Rect(7, 0, 13, 1), right.LastRect);
    }

    // --- Empty and shared cells --------------------------------------------------------------------

    [Fact]
    public void Render_EmptyCell_DoesNotThrowAndRendersNothingThere()
    {
        var a = new RecordingWidget();

        var grid = new Grid(
            rows: [SizeMode.Fixed(1), SizeMode.Fixed(1)],
            columns: [SizeMode.Fixed(1), SizeMode.Fixed(1)],
            children: [Grid.At(0, 0, a)]);

        grid.Render(DummyBuffer, new Rect(0, 0, 2, 2));

        Assert.Equal(new Rect(0, 0, 1, 1), a.LastRect);
    }

    [Fact]
    public void Render_MultipleChildrenInSameCell_LaterOverwritesEarlier()
    {
        // Not RecordingWidget: this needs an actual buffer write to prove draw order.
        var buffer = new Buffer(1, 1);
        var grid = new Grid(
            rows: [SizeMode.Fixed(1)],
            columns: [SizeMode.Fixed(1)],
            children: [Grid.At(0, 0, new Text("A")), Grid.At(0, 0, new Text("B"))]);

        grid.Render(buffer, new Rect(0, 0, 1, 1));

        Assert.Equal('B', (char)buffer[0, 0].Char.Value);
    }

    [Fact]
    public void Render_NoTracksOrChildren_DoesNotThrow()
    {
        var grid = new Grid([], [], []);

        grid.Render(DummyBuffer, new Rect(0, 0, 5, 5));
    }

    // --- Constructor / At validation -----------------------------------------------------------

    [Fact]
    public void Constructor_ChildRowOutOfRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Grid(
            rows: [SizeMode.Fixed(1)],
            columns: [SizeMode.Fixed(1)],
            children: [Grid.At(1, 0, new RecordingWidget())]));
    }

    [Fact]
    public void Constructor_ChildColumnOutOfRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Grid(
            rows: [SizeMode.Fixed(1)],
            columns: [SizeMode.Fixed(1)],
            children: [Grid.At(0, 1, new RecordingWidget())]));
    }

    [Fact]
    public void Constructor_NegativeRowOrColumn_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Grid(
            rows: [SizeMode.Fixed(1)],
            columns: [SizeMode.Fixed(1)],
            children: [Grid.At(-1, 0, new RecordingWidget())]));

        Assert.Throws<ArgumentOutOfRangeException>(() => new Grid(
            rows: [SizeMode.Fixed(1)],
            columns: [SizeMode.Fixed(1)],
            children: [Grid.At(0, -1, new RecordingWidget())]));
    }

    [Fact]
    public void Constructor_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new Grid(null!, [], []));
        Assert.Throws<ArgumentNullException>(() => new Grid([], null!, []));
        Assert.Throws<ArgumentNullException>(() => new Grid([], [], null!));
    }

    [Fact]
    public void At_NullChild_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Grid.At(0, 0, null!));
    }

    // --- Measure -----------------------------------------------------------------------------

    private static readonly Constraints Loose = Constraints.Loose(new Size(100, 100));

    [Fact]
    public void Measure_SumsPerTrackNaturalSizes()
    {
        var a = new FixedMeasureWidget(new Size(5, 2));
        var b = new FixedMeasureWidget(new Size(9, 3));
        var c = new FixedMeasureWidget(new Size(4, 6));

        var grid = new Grid(
            rows: [SizeMode.Fixed(1), SizeMode.Fixed(1)],
            columns: [SizeMode.Fixed(1), SizeMode.Fixed(1)],
            children: [Grid.At(0, 0, a), Grid.At(0, 1, b), Grid.At(1, 0, c)]);

        // Column 0's natural width = max(5, 4) = 5, column 1 = 9 → total width 14.
        // Row 0's natural height = max(2, 3) = 3, row 1 = 6 → total height 9.
        Assert.Equal(new Size(14, 9), grid.Measure(Loose));
    }

    [Fact]
    public void Measure_ClampsToConstraints()
    {
        var a = new FixedMeasureWidget(new Size(50, 50));
        var grid = new Grid(
            rows: [SizeMode.Fixed(1)],
            columns: [SizeMode.Fixed(1)],
            children: [Grid.At(0, 0, a)]);

        Assert.Equal(new Size(10, 10), grid.Measure(new Constraints(0, 10, 0, 10)));
    }

    [Fact]
    public void Measure_NoChildren_IsZero()
    {
        var grid = new Grid([SizeMode.Fixed(1)], [SizeMode.Fixed(1)], []);

        Assert.Equal(new Size(0, 0), grid.Measure(Loose));
    }
}
