namespace GitLogViewer;

internal abstract record Msg;

internal sealed record CommitsLoaded(IReadOnlyList<Commit> Commits) : Msg;

internal sealed record LoadFailed(string Error) : Msg;

internal sealed record MoveUp : Msg;

internal sealed record MoveDown : Msg;

internal sealed record Resized(int Width, int Height) : Msg;

internal sealed record QuitRequested : Msg;
