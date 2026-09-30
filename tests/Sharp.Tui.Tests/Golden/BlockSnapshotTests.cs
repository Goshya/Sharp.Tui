using Sharp.Tui.Widgets;

namespace Sharp.Tui.Tests.Golden;

public class BlockSnapshotTests
{
    [Fact]
    public void Block_PlainNoTitle_MatchesGoldenFile()
    {
        var actual = Snapshot.RenderToGrid(new Block(new Text("hi")), width: 10, height: 4);

        Snapshot.AssertMatchesGoldenFile(actual, "Block.PlainNoTitle.golden.txt");
    }

    [Fact]
    public void Block_WithTitle_MatchesGoldenFile()
    {
        var actual = Snapshot.RenderToGrid(new Block(new Text("hi"), title: "Menu"), width: 10, height: 4);

        Snapshot.AssertMatchesGoldenFile(actual, "Block.WithTitle.golden.txt");
    }

    [Fact]
    public void Block_TitleTooLongForWidth_IsTruncatedNotElided()
    {
        var actual = Snapshot.RenderToGrid(new Block(new Text("x"), title: "A Very Long Title"), width: 10, height: 3);

        Snapshot.AssertMatchesGoldenFile(actual, "Block.TitleTruncated.golden.txt");
    }

    [Fact]
    public void Block_ChildFillsInteriorExactly_MatchesGoldenFile()
    {
        // 8x2 interior, exactly matching a 2-line, 8-char-wide Text.
        var actual = Snapshot.RenderToGrid(new Block(new Text("ABCDEFGH\nIJKLMNOP")), width: 10, height: 4);

        Snapshot.AssertMatchesGoldenFile(actual, "Block.ChildFillsInterior.golden.txt");
    }

    [Fact]
    public void Block_TooNarrow_DrawsNothing()
    {
        var actual = Snapshot.RenderToGrid(new Block(new Text("x")), width: 1, height: 5);

        Snapshot.AssertMatchesGoldenFile(actual, "Block.TooNarrow.golden.txt");
    }

    [Fact]
    public void Block_TooShort_DrawsNothing()
    {
        var actual = Snapshot.RenderToGrid(new Block(new Text("x")), width: 5, height: 1);

        Snapshot.AssertMatchesGoldenFile(actual, "Block.TooShort.golden.txt");
    }
}
