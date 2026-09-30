using Sharp.Tui.Core.Rendering;
using Sharp.Tui.Layout;
using Buffer = Sharp.Tui.Core.Rendering.Buffer;

namespace Sharp.Tui.Widgets;

// Columns, header, rows (docs/SPEC.md §2.5) — built on top of Grid (#15) rather than re-deriving
// column-width solving from scratch: ColumnWidths becomes Grid's own column tracks directly, the
// header is Grid row 0, each data row one further row, and every cell is a Text. Table mainly
// adds the header/row styling and the header-row split; the layout math is entirely Grid's,
// already tested there.
public sealed record Table : Widget
{
    public IReadOnlyList<string> Headers { get; }
    public IReadOnlyList<IReadOnlyList<string>> Rows { get; }
    public IReadOnlyList<SizeMode> ColumnWidths { get; }
    public Style? HeaderStyle { get; }
    public Style? RowStyle { get; }

    public Table(
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string>> rows,
        IReadOnlyList<SizeMode>? columnWidths = null,
        Style? headerStyle = null,
        Style? rowStyle = null)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(rows);

        var widths = columnWidths ?? Enumerable.Repeat(SizeMode.Fill(1), headers.Count).ToArray();
        if (widths.Count != headers.Count)
        {
            throw new ArgumentException(
                $"ColumnWidths.Count ({widths.Count}) must equal Headers.Count ({headers.Count}).",
                nameof(columnWidths));
        }

        Headers = headers;
        Rows = rows;
        ColumnWidths = widths;
        HeaderStyle = headerStyle;
        RowStyle = rowStyle;
    }

    public override void Render(Buffer buffer, Rect area) => BuildGrid().Render(buffer, area);

    public override Size Measure(Constraints constraints) => BuildGrid().Measure(constraints);

    // Rebuilt on every call rather than cached — Grid is just a couple of lists, not expensive
    // to assemble, and a mutable cache field would sit awkwardly on a record whose whole point is
    // structural value equality. Revisit only if a benchmark actually shows this matters
    // (docs/SPEC.md §6), not before.
    private Grid BuildGrid()
    {
        var gridRows = new List<SizeMode>(Rows.Count + 1) { SizeMode.Fixed(1) }; // header row
        for (var i = 0; i < Rows.Count; i++)
            gridRows.Add(SizeMode.Fixed(1));

        var children = new List<(int Row, int Column, Widget Child)>();
        for (var c = 0; c < Headers.Count; c++)
            children.Add(Grid.At(0, c, new Text(Headers[c], HeaderStyle)));

        for (var r = 0; r < Rows.Count; r++)
        {
            var row = Rows[r] ?? [];
            // A row with fewer cells than Headers just leaves the remaining columns empty —
            // Grid already tolerates an unplaced cell, drawing nothing there, so no special
            // handling is needed beyond not placing a child past what the row actually has.
            // A row with more cells than Headers simply has its extras ignored, the same way.
            var cellCount = Math.Min(row.Count, Headers.Count);
            for (var c = 0; c < cellCount; c++)
                children.Add(Grid.At(r + 1, c, new Text(row[c], RowStyle)));
        }

        return new Grid(gridRows, ColumnWidths, children);
    }
}
