namespace Counter;

internal abstract record Msg;

internal sealed record Increment : Msg;

internal sealed record Decrement : Msg;

internal sealed record QuitRequested : Msg;
