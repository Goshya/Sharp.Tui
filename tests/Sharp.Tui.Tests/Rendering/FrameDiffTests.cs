using System.Text;
using Sharp.Tui.Core.Rendering;
using Buffer = Sharp.Tui.Core.Rendering.Buffer;

namespace Sharp.Tui.Tests.Rendering;

public class FrameDiffTests
{
    private static Cell Cell(char ch) => new(new Rune(ch), Color.Default, Color.Default, StyleFlags.None);

    // FrameDiff.Compute takes a caller-owned, reused list rather than allocating its own —
    // this wraps that for the tests below, which only care about the resulting contents.
    private static List<DiffRun> Compute(Buffer? front, Buffer back)
    {
        var runs = new List<DiffRun>();
        FrameDiff.Compute(front, back, runs);
        return runs;
    }

    [Fact]
    public void Compute_NullFront_ReturnsOneFullWidthRunPerRow()
    {
        var back = new Buffer(width: 5, height: 3);

        var runs = Compute(front: null, back);

        Assert.Equal(
            [new DiffRun(0, 0, 4), new DiffRun(1, 0, 4), new DiffRun(2, 0, 4)],
            runs);
    }

    [Fact]
    public void Compute_FrontWithDifferentDimensions_IsTreatedAsFullInvalidation()
    {
        var front = new Buffer(width: 5, height: 3);
        var back = new Buffer(width: 5, height: 4);

        var runs = Compute(front, back);

        Assert.Equal(4, runs.Count);
        Assert.All(runs, run => Assert.Equal((0, back.Width - 1), (run.StartX, run.EndX)));
    }

    [Fact]
    public void Compute_IdenticalBuffers_ReturnsNoRuns()
    {
        var front = new Buffer(width: 5, height: 3);
        front[2, 1] = Cell('X');
        var back = new Buffer(width: 5, height: 3);
        back[2, 1] = Cell('X');

        var runs = Compute(front, back);

        Assert.Empty(runs);
    }

    [Fact]
    public void Compute_SingleChangedCell_ReturnsSingleCellRun()
    {
        var front = new Buffer(width: 5, height: 3);
        var back = new Buffer(width: 5, height: 3);
        back[2, 1] = Cell('X');

        var runs = Compute(front, back);

        Assert.Equal([new DiffRun(1, 2, 2)], runs);
    }

    [Fact]
    public void Compute_AdjacentChangedCells_AreMergedIntoOneRun()
    {
        var front = new Buffer(width: 5, height: 1);
        var back = new Buffer(width: 5, height: 1);
        back[2, 0] = Cell('X');
        back[3, 0] = Cell('Y');

        var runs = Compute(front, back);

        Assert.Equal([new DiffRun(0, 2, 3)], runs);
    }

    [Fact]
    public void Compute_ChangesWithinMergeGap_AreCoalescedThroughTheUnchangedCells()
    {
        // Columns 1..4 stay unchanged, but the gap (4 cells) is within the merge threshold,
        // so the run should span the whole thing rather than splitting into two cursor moves.
        var front = new Buffer(width: 6, height: 1);
        var back = new Buffer(width: 6, height: 1);
        back[0, 0] = Cell('X');
        back[5, 0] = Cell('Y');

        var runs = Compute(front, back);

        Assert.Equal([new DiffRun(0, 0, 5)], runs);
    }

    [Fact]
    public void Compute_ChangesBeyondMergeGap_ProduceSeparateRuns()
    {
        // One column further than the merge test above: the gap is now 5 unchanged cells,
        // past the threshold, so this must split into two runs instead of one.
        var front = new Buffer(width: 7, height: 1);
        var back = new Buffer(width: 7, height: 1);
        back[0, 0] = Cell('X');
        back[6, 0] = Cell('Y');

        var runs = Compute(front, back);

        Assert.Equal([new DiffRun(0, 0, 0), new DiffRun(0, 6, 6)], runs);
    }

    [Fact]
    public void Compute_ChangesInDifferentRows_ProduceIndependentRuns()
    {
        var front = new Buffer(width: 4, height: 3);
        var back = new Buffer(width: 4, height: 3);
        back[1, 0] = Cell('X');
        back[1, 2] = Cell('Y');

        var runs = Compute(front, back);

        Assert.Equal([new DiffRun(0, 1, 1), new DiffRun(2, 1, 1)], runs);
    }

    [Fact]
    public void Compute_ChangeAtFirstAndLastColumn_IncludesBothBoundaries()
    {
        var front = new Buffer(width: 4, height: 1);
        var back = new Buffer(width: 4, height: 1);
        back[0, 0] = Cell('X');
        back[3, 0] = Cell('Y');

        var runs = Compute(front, back);

        // Width 4 means the gap between column 0 and column 3 is only 2 unchanged cells —
        // within the merge threshold — so this coalesces into one run, not two.
        Assert.Equal([new DiffRun(0, 0, 3)], runs);
    }

    [Fact]
    public void Compute_ChangeOnlyInStyle_IsStillDetected()
    {
        var front = new Buffer(width: 3, height: 1);
        var back = new Buffer(width: 3, height: 1);
        back[1, 0] = new Cell(new Rune(' '), Color.Rgb(255, 0, 0), Color.Default, StyleFlags.Bold);

        var runs = Compute(front, back);

        Assert.Equal([new DiffRun(0, 1, 1)], runs);
    }

    [Fact]
    public void Compute_ReusesCallerSuppliedList_ClearingStaleEntriesFirst()
    {
        var front = new Buffer(width: 5, height: 1);
        var back = new Buffer(width: 5, height: 1);
        back[1, 0] = Cell('X');

        var runs = new List<DiffRun>
        {
            new DiffRun(Row: 99, StartX: 0, EndX: 0), // stale entry from a previous frame
        };

        FrameDiff.Compute(front, back, runs);

        Assert.Equal([new DiffRun(0, 1, 1)], runs);
    }
}
