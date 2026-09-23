using Sharp.Tui.Core.Input;

namespace Sharp.Tui.Tests.Input;

public class ResizeWatcherTests
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(10);

    private static async Task<List<ResizeEvent>> CollectAsync(
        ResizeWatcher watcher, int count, TimeSpan overallTimeout)
    {
        using var cts = new CancellationTokenSource(overallTimeout);
        var events = new List<ResizeEvent>();

        try
        {
            await foreach (var evt in watcher.WatchViaPollingAsync(cts.Token))
            {
                events.Add(evt.AsResize);
                if (events.Count >= count)
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the watcher never reaches `count` events before the timeout —
            // callers that want "nothing happened" pass a count higher than they expect.
        }

        return events;
    }

    [Fact]
    public async Task WatchViaPolling_SizeNeverChanges_YieldsNothing()
    {
        var watcher = new ResizeWatcher(getSize: () => (80, 24), pollInterval: PollInterval);

        var events = await CollectAsync(watcher, count: 1, overallTimeout: TimeSpan.FromMilliseconds(100));

        Assert.Empty(events);
    }

    [Fact]
    public async Task WatchViaPolling_SizeChangesOnce_YieldsOneResizeEvent()
    {
        var callCount = 0;
        var watcher = new ResizeWatcher(
            getSize: () => ++callCount <= 2 ? (80, 24) : (100, 30),
            pollInterval: PollInterval);

        var events = await CollectAsync(watcher, count: 1, overallTimeout: TimeSpan.FromSeconds(2));

        Assert.Equal([new ResizeEvent(100, 30)], events);
    }

    [Fact]
    public async Task WatchViaPolling_SizeChangesThenStaysSteady_YieldsOnlyOneEventDespiteManyPolls()
    {
        var callCount = 0;
        var watcher = new ResizeWatcher(
            getSize: () => ++callCount <= 2 ? (80, 24) : (100, 30), // changes once, then holds
            pollInterval: PollInterval);

        // Let it poll well past the point where a re-firing bug would show up as extra events.
        var events = await CollectAsync(watcher, count: 5, overallTimeout: TimeSpan.FromMilliseconds(300));

        Assert.Equal([new ResizeEvent(100, 30)], events);
    }

    [Fact]
    public async Task WatchViaPolling_MultipleDistinctChanges_YieldsEachOnce()
    {
        var sizes = new (int, int)[] { (80, 24), (80, 24), (100, 30), (100, 30), (120, 40) };
        var callCount = 0;
        var watcher = new ResizeWatcher(
            getSize: () => sizes[Math.Min(callCount++, sizes.Length - 1)],
            pollInterval: PollInterval);

        var events = await CollectAsync(watcher, count: 2, overallTimeout: TimeSpan.FromSeconds(2));

        Assert.Equal([new ResizeEvent(100, 30), new ResizeEvent(120, 40)], events);
    }

    [Fact]
    public async Task WatchViaPolling_CancelledToken_StopsEnumeration()
    {
        var watcher = new ResizeWatcher(getSize: () => (80, 24), pollInterval: PollInterval);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in watcher.WatchViaPollingAsync(cts.Token))
            {
            }
        });
    }

    [Fact]
    public void Constructor_NoArguments_UsesConsoleWindowSizeAndDefaultInterval()
    {
        // Just a construction smoke test — the real Console-backed default isn't exercised
        // further here (no real console attached under `dotnet test`).
        var record = Record.Exception(() => new ResizeWatcher());

        Assert.Null(record);
    }
}
