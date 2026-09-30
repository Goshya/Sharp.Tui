using Sharp.Tui.Runtime;
using Sharp.Tui.Widgets;

namespace GitLogViewer;

internal sealed class GitLogViewerApp(string repositoryPath) : IApp<Model, Msg>
{
    // Block's own border (top+bottom) plus the Gauge footer row below the whole Row — the rows
    // ListView actually gets to show items in is less than the full terminal height by this much.
    private const int ChromeRows = 3;

    // Reading Console.WindowHeight directly here (rather than guessing and waiting for a
    // Resized message) is safe: Tui.Run already guarantees an interactive, non-redirected
    // console by the time Init() runs. A Resized message later still keeps this correct if the
    // window is actually resized — this just gets the *first* frame right too, since a resize
    // event only ever fires on an actual size *change*, never a synthetic one on startup.
    public (Model Model, Cmd<Msg> Cmd) Init() =>
        (new Model(repositoryPath, Commits: null, SelectedIndex: 0, ScrollOffset: 0, Console.WindowHeight, Error: null),
         GitLog.Load(repositoryPath));

    public (Model Model, Cmd<Msg> Cmd) Update(Model model, Msg msg)
    {
        var (next, cmd) = Apply(model, msg);
        return (next with { ScrollOffset = ClampScrollOffset(next) }, cmd);
    }

    private static (Model, Cmd<Msg>) Apply(Model model, Msg msg) => msg switch
    {
        CommitsLoaded loaded => (model with { Commits = loaded.Commits, SelectedIndex = 0, Error = null }, Cmd.None<Msg>()),
        LoadFailed failed => (model with { Error = failed.Error }, Cmd.None<Msg>()),
        MoveUp => (model with { SelectedIndex = Math.Max(0, model.SelectedIndex - 1) }, Cmd.None<Msg>()),
        MoveDown => (model with { SelectedIndex = Math.Clamp(model.SelectedIndex + 1, 0, Math.Max(0, ItemCount(model) - 1)) }, Cmd.None<Msg>()),
        Resized resized => (model with { ViewportHeight = resized.Height }, Cmd.None<Msg>()),
        QuitRequested => (model, Cmd.Quit<Msg>()),
        _ => (model, Cmd.None<Msg>()),
    };

    private static int ItemCount(Model model) => model.Commits?.Count ?? 0;

    // Keeps SelectedIndex inside the visible window, the same "scroll into view" clamp any
    // selectable-list UI needs — recomputed after every message, not just the ones that obviously
    // touch scrolling, since a Resized message changes how many rows are even visible.
    private static int ClampScrollOffset(Model model)
    {
        var visibleRows = Math.Max(1, model.ViewportHeight - ChromeRows);
        var offset = model.ScrollOffset;

        if (model.SelectedIndex < offset)
            offset = model.SelectedIndex;
        else if (model.SelectedIndex >= offset + visibleRows)
            offset = model.SelectedIndex - visibleRows + 1;

        return Math.Clamp(offset, 0, Math.Max(0, ItemCount(model) - visibleRows));
    }

    public Widget View(Model model)
    {
        if (model.Error is { } error)
        {
            return new Block(
                new Text($"Failed to load commits from \"{model.RepositoryPath}\":\n\n{error}"),
                title: "Error",
                borderStyle: Theme.Default.Border,
                titleStyle: Theme.Default.Title);
        }

        if (model.Commits is not { Count: > 0 } commits)
        {
            return new Block(
                new Text("Loading commits..."),
                title: "GitLogViewer",
                borderStyle: Theme.Default.Border,
                titleStyle: Theme.Default.Title);
        }

        var items = commits
            .Select(commit => $"{commit.Hash} {commit.Date} {commit.Author,-20} {commit.Subject}")
            .ToArray();
        var selected = commits[model.SelectedIndex];
        var fraction = (model.SelectedIndex + 1) / (double)commits.Count;

        return new Column([
            Column.Fill(1, new Row([
                Row.Fill(2, new Block(
                    new ListView(items, model.SelectedIndex, model.ScrollOffset, selectedItemStyle: Theme.Default.Selected),
                    title: "Commits",
                    borderStyle: Theme.Default.Border,
                    titleStyle: Theme.Default.Title)),
                Row.Fixed(1, new Text("")),
                Row.Fill(1, new Block(
                    new Text($"{selected.Hash}\n{selected.Author}\n{selected.Date}\n\n{selected.Subject}"),
                    title: "Details",
                    borderStyle: Theme.Default.Border,
                    titleStyle: Theme.Default.Title)),
            ])),
            Column.Fixed(1, new Gauge(fraction, filledStyle: Theme.Default.Selected)),
        ]);
    }
}
