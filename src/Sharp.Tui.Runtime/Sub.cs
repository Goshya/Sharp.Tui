namespace Sharp.Tui.Runtime;

// A long-lived source of messages (timers, file watchers, subprocess output).
public delegate IAsyncEnumerable<TMsg> Sub<TMsg>(CancellationToken ct);
