using Counter;
using Sharp.Tui.Core.Input;
using Sharp.Tui.Runtime;

Tui.Run(new CounterApp(), keyMap: key => key is { Code: KeyCode.Char, Modifiers: KeyModifiers.None }
    ? key.Char.Value switch
    {
        '+' or '=' => new Increment(),
        '-' => new Decrement(),
        'q' => new QuitRequested(),
        _ => (Msg?)null,
    }
    : null);
