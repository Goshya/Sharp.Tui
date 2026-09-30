namespace GitLogViewer;

internal sealed record Model(
    string RepositoryPath,
    IReadOnlyList<Commit>? Commits,
    int SelectedIndex,
    int ScrollOffset,
    int ViewportHeight,
    string? Error);
