using Sharp.Tui.Layout;
using Buffer = Sharp.Tui.Core.Rendering.Buffer;

namespace Sharp.Tui.Widgets;

// Table-like two-axis layout (docs/SPEC.md §2.4): rows and columns are each an independent list
// of SizeMode tracks, solved separately via LayoutSolver — a child's cell Rect is the
// intersection of its column's width-slice and its row's height-slice. No cell spanning in this
// pass: a child occupies exactly one (row, column). Two or more children placed at the same cell
// all get that cell's Rect and are drawn in list order — same as Stack, not an error, just a
// mini-stack scoped to one cell.
public sealed record Grid : Widget
{
    public IReadOnlyList<SizeMode> Rows { get; }
    public IReadOnlyList<SizeMode> Columns { get; }
    public IReadOnlyList<(int Row, int Column, Widget Child)> Children { get; }

    public Grid(
        IReadOnlyList<SizeMode> rows,
        IReadOnlyList<SizeMode> columns,
        IReadOnlyList<(int Row, int Column, Widget Child)> children)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(children);

        // A track-count mismatch is a caller bug (#15) — fail here, at construction, rather than
        // silently clip or throw later out of Render. (uint) cast turns "negative or too large"
        // into a single bounds check, same trick Buffer's own indexer uses.
        foreach (var (row, column, _) in children)
        {
            if ((uint)row >= (uint)rows.Count)
                throw new ArgumentOutOfRangeException(nameof(children), row, $"Row must be in [0, {rows.Count}).");

            if ((uint)column >= (uint)columns.Count)
                throw new ArgumentOutOfRangeException(nameof(children), column, $"Column must be in [0, {columns.Count}).");
        }

        Rows = rows;
        Columns = columns;
        Children = children;
    }

    // Pairs a child with its cell — tracks themselves are plain SizeMode values in Rows/Columns,
    // not attached per-child like Row.Fixed/Column.Fixed: one track can hold several children (one
    // per intersecting row/column), so there's nothing single to attach a SizeMode to here.
    public static (int Row, int Column, Widget Child) At(int row, int column, Widget child)
    {
        ArgumentNullException.ThrowIfNull(child);
        return (row, column, child);
    }

    public override void Render(Buffer buffer, Rect area)
    {
        // Loose, bounded by the Grid's own area — same reasoning as LinearContainer: an Auto
        // track's ideal size is independent of what any Fixed/other Auto track already claimed;
        // Solve's own remaining-budget clamp reins it in if it doesn't actually fit.
        var measureConstraints = Constraints.Loose(new Size(area.Width, area.Height));

        // An Auto column/row sizes to its widest/tallest child — not just one, unlike Row/Column
        // where every SizeMode entry corresponds to exactly one widget.
        int MeasureAutoColumn(int column) => Children
            .Where(c => c.Column == column)
            .Select(c => c.Child.Measure(measureConstraints).Width)
            .DefaultIfEmpty(0)
            .Max();

        int MeasureAutoRow(int row) => Children
            .Where(c => c.Row == row)
            .Select(c => c.Child.Measure(measureConstraints).Height)
            .DefaultIfEmpty(0)
            .Max();

        var columnWidths = LayoutSolver.Solve(area.Width, Columns, MeasureAutoColumn);
        var rowHeights = LayoutSolver.Solve(area.Height, Rows, MeasureAutoRow);
        var columnOffsets = ComputeOffsets(columnWidths);
        var rowOffsets = ComputeOffsets(rowHeights);

        foreach (var (row, column, child) in Children)
        {
            // Each cell's Rect is the intersection of its column's full-height slice and its
            // row's full-width slice — Rect.Intersect isn't just a defensive clip here (as in
            // LinearContainer), it's the actual mechanism combining two independent 1D solves
            // into one 2D Rect.
            var columnSlice = new Rect(area.X + columnOffsets[column], area.Y, columnWidths[column], area.Height);
            var rowSlice = new Rect(area.X, area.Y + rowOffsets[row], area.Width, rowHeights[row]);
            child.Render(buffer, columnSlice.Intersect(rowSlice));
        }
    }

    // Cumulative offsets for every track, computed up front rather than accumulated during a
    // single sequential pass (as LinearContainer does): children can reference any row/column, in
    // any order, so random access to "where does track i start" is needed, not just the next one.
    private static int[] ComputeOffsets(int[] sizes)
    {
        var offsets = new int[sizes.Length];
        var offset = 0;

        for (var i = 0; i < sizes.Length; i++)
        {
            offsets[i] = offset;
            offset += sizes[i];
        }

        return offsets;
    }

    // Same SizeMode-agnostic simplification as Row/Column/Stack.Measure: width is the sum of
    // each column's natural width (its widest child), height is the sum of each row's natural
    // height (its tallest child) — the two axes solved independently, then clamped together.
    public override Size Measure(Constraints constraints)
    {
        var width = 0;
        for (var column = 0; column < Columns.Count; column++)
        {
            width += Children
                .Where(c => c.Column == column)
                .Select(c => c.Child.Measure(constraints).Width)
                .DefaultIfEmpty(0)
                .Max();
        }

        var height = 0;
        for (var row = 0; row < Rows.Count; row++)
        {
            height += Children
                .Where(c => c.Row == row)
                .Select(c => c.Child.Measure(constraints).Height)
                .DefaultIfEmpty(0)
                .Max();
        }

        return new Size(
            Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
            Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight));
    }
}
