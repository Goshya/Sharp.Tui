namespace ItemBrowser;

internal sealed record Model(IReadOnlyList<(string Name, string Description)> Items, int SelectedIndex);
