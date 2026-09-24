namespace Stopwatch;

internal abstract record Msg;

internal sealed record Tick(DateTimeOffset Now) : Msg;

internal sealed record ToggleRunning : Msg;

internal sealed record Reset : Msg;

internal sealed record QuitRequested : Msg;
