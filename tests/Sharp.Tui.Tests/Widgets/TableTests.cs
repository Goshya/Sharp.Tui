using Sharp.Tui.Layout;
using Sharp.Tui.Widgets;

namespace Sharp.Tui.Tests.Widgets;

public class TableTests
{
    [Fact]
    public void Constructor_NullHeaders_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new Table(null!, []));
    }

    [Fact]
    public void Constructor_NullRows_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new Table(["A"], null!));
    }

    [Fact]
    public void Constructor_ColumnWidthsCountMismatch_ThrowsWithAClearMessage()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new Table(["Name", "Age"], [], columnWidths: [SizeMode.Fill(1)]));

        Assert.Contains("ColumnWidths", ex.Message);
        Assert.Contains("Headers", ex.Message);
    }

    [Fact]
    public void Constructor_NoColumnWidths_DefaultsToOneEqualFillPerColumn()
    {
        var table = new Table(["A", "B", "C"], []);

        Assert.Equal(3, table.ColumnWidths.Count);
    }

    [Fact]
    public void Constructor_RowWithMoreCellsThanHeaders_DoesNotThrow_ExtrasAreIgnored()
    {
        var table = new Table(["Name"], [["Alice", "extra", "cells"]]);

        // No exception building/rendering it — the extra cells past Headers.Count are silently
        // dropped, not an error.
        var buffer = new Sharp.Tui.Core.Rendering.Buffer(10, 2);
        table.Render(buffer, new Rect(0, 0, 10, 2));
    }

    // --- Measure -----------------------------------------------------------------------------

    private static readonly Constraints Loose = Constraints.Loose(new Size(100, 100));

    [Fact]
    public void Measure_DelegatesToTheUnderlyingGrid()
    {
        // 1 header row + 2 data rows = 3 rows tall; width is the sum of each column's widest
        // cell, exactly the same computation Grid.Measure already does and is tested for.
        var table = new Table(
            ["Name", "Age"],
            [["Alice", "30"], ["Bob", "25"]]);

        Assert.Equal(new Size("Alice".Length + "Age".Length, 3), table.Measure(Loose));
    }

    [Fact]
    public void Measure_ClampsToConstraints()
    {
        var table = new Table(["A very very long header"], [["A very very long cell"]]);

        Assert.Equal(new Size(10, 2), table.Measure(new Constraints(0, 10, 0, 2)));
    }
}
