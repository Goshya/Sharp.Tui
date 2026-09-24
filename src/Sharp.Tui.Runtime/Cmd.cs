namespace Sharp.Tui.Runtime;

// A side effect that eventually produces zero or one message (null = no message).
public delegate Task<TMsg?> Cmd<TMsg>(CancellationToken ct);
