using Sharp.Tui.Widgets;

namespace Sharp.Tui.Tests.Golden;

// Selection/highlighting is style-only — it never changes a single character in the grid, so it
// isn't something a plain-text golden file can show at all (a "selected" and an "unselected" run
// would render identically here). Those scenarios are unit-tested directly against Buffer cells
// instead, in ListViewTests. What's left for golden files is pure text/scrolling geometry: which
// items land on which rows.
public class ListViewSnapshotTests
{
    [Fact]
    public void ListView_ShortList_FitsEntirely_MatchesGoldenFile()
    {
        var listView = new ListView(["One", "Two", "Three"], selectedIndex: 0, scrollOffset: 0);

        var actual = Snapshot.RenderToGrid(listView, width: 8, height: 5);

        Snapshot.AssertMatchesGoldenFile(actual, "ListView.ShortListFitsEntirely.golden.txt");
    }

    [Fact]
    public void ListView_LongList_ScrolledToMiddle_MatchesGoldenFile()
    {
        var items = Enumerable.Range(0, 10).Select(i => $"Item {i}").ToArray();
        var listView = new ListView(items, selectedIndex: 4, scrollOffset: 4);

        var actual = Snapshot.RenderToGrid(listView, width: 8, height: 3);

        Snapshot.AssertMatchesGoldenFile(actual, "ListView.LongListScrolledToMiddle.golden.txt");
    }

    [Fact]
    public void ListView_EmptyItemList_MatchesGoldenFile()
    {
        var listView = new ListView([], selectedIndex: 0, scrollOffset: 0);

        var actual = Snapshot.RenderToGrid(listView, width: 8, height: 3);

        Snapshot.AssertMatchesGoldenFile(actual, "ListView.EmptyItemList.golden.txt");
    }
}
