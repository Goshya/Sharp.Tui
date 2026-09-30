using Sharp.Tui.Core.Rendering;
using Sharp.Tui.Layout;
using Sharp.Tui.Widgets;
using Buffer = Sharp.Tui.Core.Rendering.Buffer;

namespace Sharp.Tui.Tests.Widgets;

public class ListViewTests
{
    [Fact]
    public void Constructor_NullItems_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ListView(null!, 0, 0));
    }

    // --- Selection styling (not expressible in a plain-text golden file — see
    // ListViewSnapshotTests) --------------------------------------------------------------------

    [Fact]
    public void Render_SelectedRow_UsesSelectedItemStyle_OthersUseItemStyle()
    {
        var buffer = new Buffer(5, 3);
        var itemStyle = new Style(Foreground: Color.Named(1));
        var selectedStyle = new Style(Foreground: Color.Named(2));
        var listView = new ListView(["A", "B", "C"], selectedIndex: 1, scrollOffset: 0, itemStyle, selectedStyle);

        listView.Render(buffer, new Rect(0, 0, 5, 3));

        Assert.Equal(Color.Named(1), buffer[0, 0].Foreground); // row 0: "A", not selected
        Assert.Equal(Color.Named(2), buffer[0, 1].Foreground); // row 1: "B", selected
        Assert.Equal(Color.Named(1), buffer[0, 2].Foreground); // row 2: "C", not selected
    }

    [Fact]
    public void Render_SelectedIndexScrolledOutOfView_NothingIsHighlighted()
    {
        var buffer = new Buffer(5, 2);
        var itemStyle = new Style(Foreground: Color.Named(1));
        var selectedStyle = new Style(Foreground: Color.Named(2));
        // SelectedIndex 0 is scrolled above the visible window (rows 2-3 show items 2 and 3).
        var listView = new ListView(["A", "B", "C", "D"], selectedIndex: 0, scrollOffset: 2, itemStyle, selectedStyle);

        listView.Render(buffer, new Rect(0, 0, 5, 2));

        Assert.Equal(Color.Named(1), buffer[0, 0].Foreground);
        Assert.Equal(Color.Named(1), buffer[0, 1].Foreground);
    }

    // --- Out-of-range SelectedIndex/ScrollOffset ------------------------------------------------

    [Fact]
    public void Render_NegativeScrollOffset_DoesNotThrow_LeadingRowsAreBlank()
    {
        var buffer = new Buffer(5, 3);

        new ListView(["A", "B"], selectedIndex: 0, scrollOffset: -1).Render(buffer, new Rect(0, 0, 5, 3));

        Assert.Equal(' ', (char)buffer[0, 0].Char.Value); // itemIndex -1: blank
        Assert.Equal('A', (char)buffer[0, 1].Char.Value); // itemIndex 0
        Assert.Equal('B', (char)buffer[0, 2].Char.Value); // itemIndex 1
    }

    [Fact]
    public void Render_ScrollOffsetPastEnd_DoesNotThrow_EveryRowIsBlank()
    {
        var buffer = new Buffer(5, 2);

        new ListView(["A"], selectedIndex: 0, scrollOffset: 50).Render(buffer, new Rect(0, 0, 5, 2));

        Assert.Equal(' ', (char)buffer[0, 0].Char.Value);
        Assert.Equal(' ', (char)buffer[0, 1].Char.Value);
    }

    [Fact]
    public void Render_NegativeSelectedIndex_DoesNotThrow()
    {
        var buffer = new Buffer(5, 1);

        new ListView(["A"], selectedIndex: -5, scrollOffset: 0).Render(buffer, new Rect(0, 0, 5, 1));

        Assert.Equal('A', (char)buffer[0, 0].Char.Value);
    }

    // --- Measure -----------------------------------------------------------------------------

    private static readonly Constraints Loose = Constraints.Loose(new Size(100, 100));

    [Fact]
    public void Measure_WidestItemAndItemCount()
    {
        var listView = new ListView(["short", "a much longer item"], selectedIndex: 0, scrollOffset: 0);

        Assert.Equal(new Size("a much longer item".Length, 2), listView.Measure(Loose));
    }

    [Fact]
    public void Measure_EmptyItems_IsZero()
    {
        var listView = new ListView([], selectedIndex: 0, scrollOffset: 0);

        Assert.Equal(new Size(0, 0), listView.Measure(Loose));
    }

    [Fact]
    public void Measure_ClampsToConstraints()
    {
        var listView = new ListView(["a very very very long item"], selectedIndex: 0, scrollOffset: 0);

        Assert.Equal(new Size(10, 1), listView.Measure(new Constraints(0, 10, 0, 1)));
    }
}
