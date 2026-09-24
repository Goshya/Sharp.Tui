using Sharp.Tui.Core.Input;
using Sharp.Tui.Runtime;
using Stopwatch;

Tui.Run(
    new StopwatchApp(),
    keyMap: key => key is { Code: KeyCode.Char, Modifiers: KeyModifiers.None }
        ? key.Char.Value switch
        {
            ' ' => new ToggleRunning(),
            'r' => new Reset(),
            'q' => new QuitRequested(),
            _ => (Msg?)null,
        }
        : null,
    subscriptions: [Sub.Tick(TimeSpan.FromMilliseconds(100), now => (Msg)new Tick(now))]);
