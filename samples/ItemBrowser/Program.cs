using ItemBrowser;
using Sharp.Tui.Core.Input;
using Sharp.Tui.Runtime;

Tui.Run(new ItemBrowserApp(), keyMap: key => key switch
{
    { Code: KeyCode.Up, Modifiers: KeyModifiers.None } => new MoveUp(),
    { Code: KeyCode.Down, Modifiers: KeyModifiers.None } => new MoveDown(),
    { Code: KeyCode.Char, Modifiers: KeyModifiers.None } when key.Char.Value == 'q' => new QuitRequested(),
    _ => (Msg?)null,
});
