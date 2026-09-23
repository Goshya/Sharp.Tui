using Sharp.Tui.Core.Input;

namespace Sharp.Tui.Tests.Input;

public class EscTimeoutDeciderTests
{
    private sealed class FakeTimeProvider : TimeProvider
    {
        private long _ticks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;

        public void Advance(TimeSpan by) => _ticks += by.Ticks;
    }

    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(50);

    [Fact]
    public void Observe_LoneEscByte_ReportsPending()
    {
        var time = new FakeTimeProvider();
        var decider = new EscTimeoutDecider(time, Timeout);

        var isPending = decider.Observe([0x1B], ParseResult.NeedMoreBytes);

        Assert.True(isPending);
        Assert.False(decider.HasTimedOut);
    }

    [Fact]
    public void Observe_MultiByteSequenceInProgress_DoesNotReportPending()
    {
        var time = new FakeTimeProvider();
        var decider = new EscTimeoutDecider(time, Timeout);

        // ESC[ — a real sequence in progress, not an ambiguous lone Esc.
        var isPending = decider.Observe([0x1B, (byte)'['], ParseResult.NeedMoreBytes);

        Assert.False(isPending);
        time.Advance(TimeSpan.FromSeconds(10));
        Assert.False(decider.HasTimedOut);
    }

    [Fact]
    public void Observe_CompleteResult_DoesNotReportPending()
    {
        var time = new FakeTimeProvider();
        var decider = new EscTimeoutDecider(time, Timeout);

        var isPending = decider.Observe([(byte)'a'], ParseResult.Complete);

        Assert.False(isPending);
    }

    [Fact]
    public void HasTimedOut_BeforeTimeoutElapses_IsFalse()
    {
        var time = new FakeTimeProvider();
        var decider = new EscTimeoutDecider(time, Timeout);
        decider.Observe([0x1B], ParseResult.NeedMoreBytes);

        time.Advance(TimeSpan.FromMilliseconds(49));

        Assert.False(decider.HasTimedOut);
    }

    [Fact]
    public void HasTimedOut_AfterTimeoutElapses_IsTrue()
    {
        var time = new FakeTimeProvider();
        var decider = new EscTimeoutDecider(time, Timeout);
        decider.Observe([0x1B], ParseResult.NeedMoreBytes);

        time.Advance(TimeSpan.FromMilliseconds(51));

        Assert.True(decider.HasTimedOut);
    }

    [Fact]
    public void HasTimedOut_ExactlyAtTimeout_IsTrue()
    {
        var time = new FakeTimeProvider();
        var decider = new EscTimeoutDecider(time, Timeout);
        decider.Observe([0x1B], ParseResult.NeedMoreBytes);

        time.Advance(Timeout);

        Assert.True(decider.HasTimedOut);
    }

    [Fact]
    public void Observe_MoreBytesArriveAfterPendingEsc_ClearsPendingState()
    {
        var time = new FakeTimeProvider();
        var decider = new EscTimeoutDecider(time, Timeout);
        decider.Observe([0x1B], ParseResult.NeedMoreBytes);

        // A second byte arrived — this is now a real sequence (or resolved), not a lone Esc.
        decider.Observe([0x1B, (byte)'['], ParseResult.NeedMoreBytes);

        time.Advance(TimeSpan.FromSeconds(10));
        Assert.False(decider.HasTimedOut);
    }

    [Fact]
    public void Observe_RepeatedForSameStillPendingEsc_DoesNotResetTheClock()
    {
        // Simulates a reader loop re-checking on every iteration while no new bytes have
        // arrived — the pending timestamp must stick to the *first* observation, or the
        // timeout would never fire as long as the loop keeps polling.
        var time = new FakeTimeProvider();
        var decider = new EscTimeoutDecider(time, Timeout);

        decider.Observe([0x1B], ParseResult.NeedMoreBytes);
        time.Advance(TimeSpan.FromMilliseconds(30));
        decider.Observe([0x1B], ParseResult.NeedMoreBytes); // still just the one byte, re-observed
        time.Advance(TimeSpan.FromMilliseconds(30)); // total: 60ms since the first observation

        Assert.True(decider.HasTimedOut);
    }

    [Fact]
    public void RemainingTime_NothingPending_IsNull()
    {
        var time = new FakeTimeProvider();
        var decider = new EscTimeoutDecider(time, Timeout);

        Assert.Null(decider.RemainingTime);
    }

    [Fact]
    public void RemainingTime_PartwayThroughTimeout_ReflectsWhatIsLeft()
    {
        var time = new FakeTimeProvider();
        var decider = new EscTimeoutDecider(time, Timeout);
        decider.Observe([0x1B], ParseResult.NeedMoreBytes);

        time.Advance(TimeSpan.FromMilliseconds(20));

        Assert.Equal(TimeSpan.FromMilliseconds(30), decider.RemainingTime);
    }

    [Fact]
    public void Observe_SkipResult_ClearsPendingState()
    {
        var time = new FakeTimeProvider();
        var decider = new EscTimeoutDecider(time, Timeout);
        decider.Observe([0x1B], ParseResult.NeedMoreBytes);

        decider.Observe([0xFF], ParseResult.Skip);

        time.Advance(TimeSpan.FromSeconds(10));
        Assert.False(decider.HasTimedOut);
    }

    [Fact]
    public void NewlyPendingEscAfterAPreviousOneResolved_StartsItsOwnFreshTimeout()
    {
        var time = new FakeTimeProvider();
        var decider = new EscTimeoutDecider(time, Timeout);

        decider.Observe([0x1B], ParseResult.NeedMoreBytes);
        time.Advance(TimeSpan.FromMilliseconds(40));
        decider.Observe([(byte)'a'], ParseResult.Complete); // first Esc resolved some other way

        // A second, unrelated lone Esc shows up later — it must get its own fresh window,
        // not inherit however much of the first one's timeout had already elapsed.
        decider.Observe([0x1B], ParseResult.NeedMoreBytes);
        time.Advance(TimeSpan.FromMilliseconds(40));

        Assert.False(decider.HasTimedOut);
    }
}
