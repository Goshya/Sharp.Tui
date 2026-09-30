using Sharp.Tui.Widgets;

namespace Sharp.Tui.Tests.Golden;

public class GaugeSnapshotTests
{
    [Fact]
    public void Gauge_Empty_MatchesGoldenFile()
    {
        var actual = Snapshot.RenderToGrid(new Gauge(0), width: 10, height: 1);

        Snapshot.AssertMatchesGoldenFile(actual, "Gauge.Empty.golden.txt");
    }

    [Fact]
    public void Gauge_Full_MatchesGoldenFile()
    {
        var actual = Snapshot.RenderToGrid(new Gauge(1), width: 10, height: 1);

        Snapshot.AssertMatchesGoldenFile(actual, "Gauge.Full.golden.txt");
    }

    [Fact]
    public void Gauge_HalfInOddWidth_PinsTheRoundingRule()
    {
        // Width 7, Fraction 0.5: 7 * 0.5 = 3.5, rounds away from zero to 4 filled columns.
        var actual = Snapshot.RenderToGrid(new Gauge(0.5), width: 7, height: 1);

        Snapshot.AssertMatchesGoldenFile(actual, "Gauge.HalfOddWidth.golden.txt");
    }

    [Fact]
    public void Gauge_WithLabel_MatchesGoldenFile()
    {
        var actual = Snapshot.RenderToGrid(new Gauge(0.5, label: "50%"), width: 10, height: 1);

        Snapshot.AssertMatchesGoldenFile(actual, "Gauge.WithLabel.golden.txt");
    }

    [Fact]
    public void Gauge_TallerThanOneRow_FillsEveryRow()
    {
        var actual = Snapshot.RenderToGrid(new Gauge(0.5), width: 8, height: 3);

        Snapshot.AssertMatchesGoldenFile(actual, "Gauge.TallerThanOneRow.golden.txt");
    }
}
