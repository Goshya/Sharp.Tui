using System.Runtime.CompilerServices;

namespace Sharp.Tui.Runtime;

public static class Sub
{
    // The minimal long-lived Sub: a message every `interval`, built by `selector` from the time of
    // the tick. TimeProvider is injectable, same as everywhere else time matters in this codebase.
    public static Sub<TMsg> Tick<TMsg>(TimeSpan interval, Func<DateTimeOffset, TMsg> selector, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);

        var time = timeProvider ?? TimeProvider.System;
        return ct => TickAsync(interval, selector, time, ct);
    }

    private static async IAsyncEnumerable<TMsg> TickAsync<TMsg>(
        TimeSpan interval,
        Func<DateTimeOffset, TMsg> selector,
        TimeProvider time,
        [EnumeratorCancellation] CancellationToken ct)
    {
        using var timer = new PeriodicTimer(interval, time);
        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            yield return selector(time.GetUtcNow());
    }
}
