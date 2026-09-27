using Sharp.Tui.Layout;
using Sharp.Tui.Widgets;
using Buffer = Sharp.Tui.Core.Rendering.Buffer;

namespace Sharp.Tui.Tests.Widgets;

public class RowColumnTests
{
    private static readonly Buffer DummyBuffer = new(1, 1); // never touched — RecordingWidget ignores it

    // --- The nested tree from #3's "Done when" --------------------------------------------------

    [Fact]
    public void Render_RowOfColumnAndFixed_MatchesTheIssueExample()
    {
        // Row { Column { Fixed(10), Fill(1) }, Fixed(20) }, in a Row 50x24. The Column's own
        // received Rect isn't observed directly — it doesn't record itself — but it's fully
        // implied by what its own children (top/bottom) get: same X/Width, split Y/Height.
        var top = new RecordingWidget();
        var bottom = new RecordingWidget();
        var column = new Column([Column.Fixed(10, top), Column.Fill(1, bottom)]);
        var right = new RecordingWidget();
        var row = new Row([Row.Fill(1, column), Row.Fixed(20, right)]);

        row.Render(DummyBuffer, new Rect(0, 0, 50, 24));

        // Row: Fixed(20) takes 20, Fill(1) gets the remaining 30 — that's the Column's area.
        Assert.Equal(new Rect(30, 0, 20, 24), right.LastRect);

        // Column, handed Rect(0, 0, 30, 24): Fixed(10) takes 10 of the height, Fill(1) gets the
        // remaining 14 — both keep the Column's full X/Width (0, 30).
        Assert.Equal(new Rect(0, 0, 30, 10), top.LastRect);
        Assert.Equal(new Rect(0, 10, 30, 14), bottom.LastRect);
    }

    // --- Row ---------------------------------------------------------------------------------

    [Fact]
    public void Row_FixedOnly_PlacesChildrenSideBySideAtTheirOwnWidth()
    {
        var a = new RecordingWidget();
        var b = new RecordingWidget();
        var row = new Row([Row.Fixed(10, a), Row.Fixed(20, b)]);

        row.Render(DummyBuffer, new Rect(0, 0, 40, 5));

        Assert.Equal(new Rect(0, 0, 10, 5), a.LastRect);
        Assert.Equal(new Rect(10, 0, 20, 5), b.LastRect);
    }

    [Fact]
    public void Row_FillOnly_SplitsWidthEvenly()
    {
        var a = new RecordingWidget();
        var b = new RecordingWidget();
        var row = new Row([Row.Fill(1, a), Row.Fill(1, b)]);

        row.Render(DummyBuffer, new Rect(2, 3, 10, 5));

        Assert.Equal(new Rect(2, 3, 5, 5), a.LastRect);
        Assert.Equal(new Rect(7, 3, 5, 5), b.LastRect);
    }

    [Fact]
    public void Row_MixedFixedAndFill_FillGetsWhatIsLeft()
    {
        var a = new RecordingWidget();
        var b = new RecordingWidget();
        var row = new Row([Row.Fixed(10, a), Row.Fill(1, b)]);

        row.Render(DummyBuffer, new Rect(0, 0, 30, 5));

        Assert.Equal(new Rect(0, 0, 10, 5), a.LastRect);
        Assert.Equal(new Rect(10, 0, 20, 5), b.LastRect);
    }

    [Fact]
    public void Row_FixedSumExceedingAvailable_LaterChildGetsZeroWidth()
    {
        var a = new RecordingWidget();
        var b = new RecordingWidget();
        var row = new Row([Row.Fixed(8, a), Row.Fixed(8, b)]);

        row.Render(DummyBuffer, new Rect(0, 0, 10, 5));

        Assert.Equal(new Rect(0, 0, 8, 5), a.LastRect);
        Assert.Equal(new Rect(8, 0, 2, 5), b.LastRect);
    }

    [Fact]
    public void Row_UnevenFillSplit_UsesLargestRemainderAndStaysWithinArea()
    {
        var a = new RecordingWidget();
        var b = new RecordingWidget();
        var c = new RecordingWidget();
        var row = new Row([Row.Fill(1, a), Row.Fill(1, b), Row.Fill(1, c)]);

        row.Render(DummyBuffer, new Rect(0, 0, 10, 1));

        Assert.Equal(4, a.LastRect!.Value.Width);
        Assert.Equal(3, b.LastRect!.Value.Width);
        Assert.Equal(3, c.LastRect!.Value.Width);
        Assert.Equal(10, a.LastRect.Value.Width + b.LastRect.Value.Width + c.LastRect.Value.Width);
    }

    [Fact]
    public void Row_NoChildren_DoesNotThrow()
    {
        var row = new Row([]);

        row.Render(DummyBuffer, new Rect(0, 0, 10, 10));
    }

    [Fact]
    public void Row_ChildRect_IsClippedToParentArea()
    {
        // A child area that would (hypothetically) fall outside `area` never reaches the child
        // unclipped — regression guard for the Intersect call in LinearContainer.Render.
        var a = new RecordingWidget();
        var row = new Row([Row.Fixed(999, a)]);

        row.Render(DummyBuffer, new Rect(0, 0, 10, 5));

        Assert.Equal(new Rect(0, 0, 10, 5), a.LastRect);
    }

    // --- Column ------------------------------------------------------------------------------

    [Fact]
    public void Column_FixedOnly_StacksChildrenAtTheirOwnHeight()
    {
        var a = new RecordingWidget();
        var b = new RecordingWidget();
        var column = new Column([Column.Fixed(3, a), Column.Fixed(4, b)]);

        column.Render(DummyBuffer, new Rect(0, 0, 5, 20));

        Assert.Equal(new Rect(0, 0, 5, 3), a.LastRect);
        Assert.Equal(new Rect(0, 3, 5, 4), b.LastRect);
    }

    [Fact]
    public void Column_FillOnly_SplitsHeightEvenly()
    {
        var a = new RecordingWidget();
        var b = new RecordingWidget();
        var column = new Column([Column.Fill(1, a), Column.Fill(1, b)]);

        column.Render(DummyBuffer, new Rect(1, 2, 5, 10));

        Assert.Equal(new Rect(1, 2, 5, 5), a.LastRect);
        Assert.Equal(new Rect(1, 7, 5, 5), b.LastRect);
    }

    [Fact]
    public void Column_MixedFixedAndFill_FillGetsWhatIsLeft()
    {
        var header = new RecordingWidget();
        var body = new RecordingWidget();
        var column = new Column([Column.Fixed(1, header), Column.Fill(1, body)]);

        column.Render(DummyBuffer, new Rect(0, 0, 20, 10));

        Assert.Equal(new Rect(0, 0, 20, 1), header.LastRect);
        Assert.Equal(new Rect(0, 1, 20, 9), body.LastRect);
    }

    [Fact]
    public void Column_Percent_IsOfAvailableHeight()
    {
        var a = new RecordingWidget();
        var b = new RecordingWidget();
        var column = new Column([Column.Percent(30, a), Column.Fill(1, b)]);

        column.Render(DummyBuffer, new Rect(0, 0, 5, 100));

        Assert.Equal(30, a.LastRect!.Value.Height);
        Assert.Equal(70, b.LastRect!.Value.Height);
    }

    [Fact]
    public void Column_NoChildren_DoesNotThrow()
    {
        var column = new Column([]);

        column.Render(DummyBuffer, new Rect(0, 0, 10, 10));
    }

    // --- Auto --------------------------------------------------------------------------------
    // An Auto child's Rect is dictated by its own Measure result, not by anything the container
    // decides — Row/Column don't even look at the child's content, just at what Measure reports.

    [Fact]
    public void Row_AutoChild_WidthMatchesItsOwnMeasureResult()
    {
        var header = new FixedMeasureWidget(new Size(7, 1));
        var body = new RecordingWidget();
        var row = new Row([Row.Auto(header), Row.Fill(1, body)]);

        row.Render(DummyBuffer, new Rect(0, 0, 30, 5));

        Assert.Equal(new Rect(7, 0, 23, 5), body.LastRect);
    }

    [Fact]
    public void Row_AutoChild_ExceedingAvailable_IsClampedNotOverflowing()
    {
        var oversized = new FixedMeasureWidget(new Size(999, 1));
        var sibling = new RecordingWidget();
        var row = new Row([Row.Auto(oversized), Row.Fill(1, sibling)]);

        row.Render(DummyBuffer, new Rect(0, 0, 10, 5));

        Assert.Equal(new Rect(0, 0, 10, 5), oversized.LastRect);
        Assert.Equal(new Rect(10, 0, 0, 5), sibling.LastRect);
    }

    [Fact]
    public void Column_AutoChild_HeightMatchesItsOwnMeasureResult()
    {
        var header = new FixedMeasureWidget(new Size(1, 3));
        var body = new RecordingWidget();
        var column = new Column([Column.Auto(header), Column.Fill(1, body)]);

        column.Render(DummyBuffer, new Rect(0, 0, 5, 20));

        Assert.Equal(new Rect(0, 3, 5, 17), body.LastRect);
    }

    // --- Constructor -----------------------------------------------------------------------------

    [Fact]
    public void Constructors_NullChildren_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new Row(null!));
        Assert.Throws<ArgumentNullException>(() => new Column(null!));
    }

    [Fact]
    public void Factories_NullChild_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => Row.Fixed(1, null!));
        Assert.Throws<ArgumentNullException>(() => Row.Percent(1, null!));
        Assert.Throws<ArgumentNullException>(() => Row.Fill(1, null!));
        Assert.Throws<ArgumentNullException>(() => Row.Auto(null!));
        Assert.Throws<ArgumentNullException>(() => Column.Fixed(1, null!));
        Assert.Throws<ArgumentNullException>(() => Column.Percent(1, null!));
        Assert.Throws<ArgumentNullException>(() => Column.Fill(1, null!));
        Assert.Throws<ArgumentNullException>(() => Column.Auto(null!));
    }

    // --- Measure -----------------------------------------------------------------------------
    // A container's own Measure ignores each child's SizeMode entirely (Fixed/Percent/Fill only
    // matter for Render's actual Solve pass) — it just asks every child its own natural size and
    // combines them: summed along the container's own axis, maxed along the cross axis.

    private static readonly Constraints Loose = Constraints.Loose(new Size(100, 100));

    [Fact]
    public void Row_Measure_SumsWidthsAndTakesMaxHeight()
    {
        var a = new FixedMeasureWidget(new Size(5, 2));
        var b = new FixedMeasureWidget(new Size(3, 7));
        var row = new Row([Row.Fixed(1, a), Row.Fill(1, b)]);

        Assert.Equal(new Size(8, 7), row.Measure(Loose));
    }

    [Fact]
    public void Row_Measure_NoChildren_IsZero()
    {
        var row = new Row([]);

        Assert.Equal(new Size(0, 0), row.Measure(Loose));
    }

    [Fact]
    public void Row_Measure_ClampsToConstraints()
    {
        var a = new FixedMeasureWidget(new Size(50, 50));
        var row = new Row([Row.Fixed(1, a)]);

        Assert.Equal(new Size(10, 10), row.Measure(new Constraints(0, 10, 0, 10)));
    }

    [Fact]
    public void Column_Measure_SumsHeightsAndTakesMaxWidth()
    {
        var a = new FixedMeasureWidget(new Size(5, 2));
        var b = new FixedMeasureWidget(new Size(9, 3));
        var column = new Column([Column.Fixed(1, a), Column.Fill(1, b)]);

        Assert.Equal(new Size(9, 5), column.Measure(Loose));
    }

    [Fact]
    public void Column_Measure_NoChildren_IsZero()
    {
        var column = new Column([]);

        Assert.Equal(new Size(0, 0), column.Measure(Loose));
    }

    [Fact]
    public void Column_Measure_ClampsToConstraints()
    {
        var a = new FixedMeasureWidget(new Size(50, 50));
        var column = new Column([Column.Fixed(1, a)]);

        Assert.Equal(new Size(10, 10), column.Measure(new Constraints(0, 10, 0, 10)));
    }
}
