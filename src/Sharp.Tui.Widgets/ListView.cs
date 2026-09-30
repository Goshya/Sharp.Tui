using System.Text;
using Sharp.Tui.Core.Rendering;
using Sharp.Tui.Layout;
using Buffer = Sharp.Tui.Core.Rendering.Buffer;

namespace Sharp.Tui.Widgets;

// A selectable, scrollable list (docs/SPEC.md §2.5) — the first widget where "pure function of
// props" actually matters: SelectedIndex/ScrollOffset are plain numbers passed in, exactly like
// Model.Count in Counter, not state ListView owns or advances itself. It never reads input and
// has no key bindings of its own — moving the selection on arrow keys is the app's own
// Update/keyMap, the same as Increment/Decrement; baking that into the widget would break the
// pure-function-of-props model every widget has held to since M2.
public sealed record ListView : Widget
{
    public IReadOnlyList<string> Items { get; }
    public int SelectedIndex { get; }
    public int ScrollOffset { get; }
    public Style? ItemStyle { get; }
    public Style? SelectedItemStyle { get; }

    public ListView(
        IReadOnlyList<string> items,
        int selectedIndex,
        int scrollOffset,
        Style? itemStyle = null,
        Style? selectedItemStyle = null)
    {
        ArgumentNullException.ThrowIfNull(items);

        Items = items;
        SelectedIndex = selectedIndex;
        ScrollOffset = scrollOffset;
        ItemStyle = itemStyle;
        SelectedItemStyle = selectedItemStyle;
    }

    public override void Render(Buffer buffer, Rect area)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        // One bounds check covers three cases at once: a negative ScrollOffset (the first few
        // rows' itemIndex is negative), a ScrollOffset past the end of Items (every row's
        // itemIndex overruns), and an empty Items list (every itemIndex overruns immediately) —
        // all of them just mean "nothing to draw on this row", not an error.
        for (var row = 0; row < area.Height; row++)
        {
            var itemIndex = ScrollOffset + row;
            if (itemIndex < 0 || itemIndex >= Items.Count)
                continue;

            // Delegates to Text rather than walking Runes by hand again — same clipping and
            // null-content handling, for free, on a single-row Rect.
            var style = itemIndex == SelectedIndex ? SelectedItemStyle : ItemStyle;
            new Text(Items[itemIndex], style).Render(buffer, new Rect(area.X, area.Y + row, area.Width, 1));
        }
    }

    // Unlike Gauge, a ListView's content gives it a real natural size: the widest item (in
    // Runes) and enough rows to show every item without scrolling — so a short menu inside
    // Column.Auto(...) sizes to fit exactly, rather than always needing an explicit Fill.
    // Items aren't expected to contain '\n' themselves (Render treats each as one row); a rogue
    // newline just inflates this width estimate slightly, the same low-priority edge case
    // Render already tolerates by truncating at the row boundary.
    public override Size Measure(Constraints constraints)
    {
        var width = 0;
        foreach (var item in Items)
            width = Math.Max(width, (item ?? string.Empty).EnumerateRunes().Count());

        return new Size(
            Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
            Math.Clamp(Items.Count, constraints.MinHeight, constraints.MaxHeight));
    }
}
