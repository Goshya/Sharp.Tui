using GitLogViewer;
using Sharp.Tui.Core.Input;
using Sharp.Tui.Runtime;

// No path argument: defaults to the current directory — running this from the repo root shows
// Sharp.Tui's own commit history.
var repositoryPath = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();

Tui.Run(
    new GitLogViewerApp(repositoryPath),
    keyMap: key => key switch
    {
        { Code: KeyCode.Up, Modifiers: KeyModifiers.None } => new MoveUp(),
        { Code: KeyCode.Down, Modifiers: KeyModifiers.None } => new MoveDown(),
        { Code: KeyCode.Char, Modifiers: KeyModifiers.None } when key.Char.Value == 'q' => new QuitRequested(),
        _ => (Msg?)null,
    },
    resizeMap: resize => new Resized(resize.Width, resize.Height));
