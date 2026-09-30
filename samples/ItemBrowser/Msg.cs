namespace ItemBrowser;

internal abstract record Msg;

internal sealed record MoveUp : Msg;

internal sealed record MoveDown : Msg;

internal sealed record QuitRequested : Msg;
