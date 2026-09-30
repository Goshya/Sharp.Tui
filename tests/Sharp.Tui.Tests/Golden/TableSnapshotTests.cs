using Sharp.Tui.Layout;
using Sharp.Tui.Widgets;

namespace Sharp.Tui.Tests.Golden;

public class TableSnapshotTests
{
    [Fact]
    public void Table_EvenColumnWidths_MatchesGoldenFile()
    {
        var table = new Table(
            ["Name", "Age"],
            [["Alice", "30"], ["Bob", "25"]]);

        var actual = Snapshot.RenderToGrid(table, width: 10, height: 4);

        Snapshot.AssertMatchesGoldenFile(actual, "Table.EvenColumnWidths.golden.txt");
    }

    [Fact]
    public void Table_MixedSizeModeColumnWidths_MatchesGoldenFile()
    {
        var table = new Table(
            ["Name", "Age"],
            [["Alice", "30"], ["Bob", "25"]],
            columnWidths: [SizeMode.Fixed(6), SizeMode.Fill(1)]);

        var actual = Snapshot.RenderToGrid(table, width: 10, height: 4);

        Snapshot.AssertMatchesGoldenFile(actual, "Table.MixedColumnWidths.golden.txt");
    }

    [Fact]
    public void Table_RowWithFewerCellsThanHeaders_MissingCellsAreBlank()
    {
        var table = new Table(
            ["Name", "Age", "City"],
            [["Alice", "30"]]); // "City" cell missing for this row

        var actual = Snapshot.RenderToGrid(table, width: 12, height: 2);

        Snapshot.AssertMatchesGoldenFile(actual, "Table.RowMissingCells.golden.txt");
    }

    [Fact]
    public void Table_EmptyRows_HeaderOnly_MatchesGoldenFile()
    {
        var table = new Table(["Name", "Age"], []);

        var actual = Snapshot.RenderToGrid(table, width: 10, height: 3);

        Snapshot.AssertMatchesGoldenFile(actual, "Table.EmptyRowsHeaderOnly.golden.txt");
    }
}
