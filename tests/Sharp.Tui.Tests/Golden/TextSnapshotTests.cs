using Sharp.Tui.Widgets;

namespace Sharp.Tui.Tests.Golden;

// Proves the golden-file harness itself, end to end, against Text — already implemented, so the
// only thing under test here is Snapshot, not any new rendering behavior.
public class TextSnapshotTests
{
    [Fact]
    public void Text_SingleLine_MatchesGoldenFile()
    {
        var actual = Snapshot.RenderToGrid(new Text("Hello, Sharp.Tui!"), width: 20, height: 3);

        Snapshot.AssertMatchesGoldenFile(actual, "Text.SingleLine.golden.txt");
    }
}
