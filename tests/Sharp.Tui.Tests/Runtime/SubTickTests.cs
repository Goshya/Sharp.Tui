using Sharp.Tui.Runtime;

namespace Sharp.Tui.Tests.Runtime;

public class SubTickTests
{
    [Fact]
    public async Task Tick_YieldsSelectorResultsRepeatedly()
    {
        var sub = Sub.Tick(TimeSpan.FromMilliseconds(10), now => now);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var ticks = new List<DateTimeOffset>();

        await foreach (var tick in sub(cts.Token))
        {
            ticks.Add(tick);
            if (ticks.Count == 3)
                break;
        }

        Assert.Equal(3, ticks.Count);
        Assert.True(ticks[0] <= ticks[1] && ticks[1] <= ticks[2]);
    }

    [Fact]
    public async Task Tick_StopsWhenCancelled()
    {
        var sub = Sub.Tick(TimeSpan.FromMilliseconds(10), _ => 0);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in sub(cts.Token))
            {
            }
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Tick_NonPositiveInterval_Throws(int milliseconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Sub.Tick(TimeSpan.FromMilliseconds(milliseconds), _ => 0));
    }

    [Fact]
    public void Tick_NullSelector_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Sub.Tick<int>(TimeSpan.FromMilliseconds(10), null!));
    }
}
