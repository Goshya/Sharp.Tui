# Sharp.Tui

[![CI](https://github.com/Goshya/Sharp.Tui/actions/workflows/ci.yml/badge.svg)](https://github.com/Goshya/Sharp.Tui/actions/workflows/ci.yml)

A small, dependency-free TUI (terminal UI) framework for .NET, built around **The Elm
Architecture**: an immutable `Model`, a pure `Update` function, a pure `View` function, and side
effects modeled explicitly as `Cmd`/`Sub`. Not a reskin of an existing library — a genuinely
different way to structure a terminal app in C#.

![GitLogViewer demo](docs/demo.gif)

## Why another TUI library

| | Sharp.Tui | Terminal.Gui | Spectre.Console |
|---|---|---|---|
| Architecture | The Elm Architecture: immutable `Model`, pure `Update`/`View` | Event-driven, stateful views | Styled *output* (tables, panels, markup) — not a full interactive runtime |
| Layout engine | `Row`/`Column`/`Grid`, `Fixed`/`Percent`/`Fill`/`Auto` sizing | `Pos`/`Dim` constraint system | None — not built for interactive layout |
| Runtime dependencies | Zero | Has some | Zero |
| NativeAOT / trim | Clean from day one — no reflection, no `Activator.CreateInstance`, anywhere in `Core`/`Layout`/`Widgets`/`Runtime` | Not a stated goal | The library itself is AOT-friendly, but it isn't an interactive runtime to begin with |

This table sticks to verifiable, architectural facts, not a subjective "better than" — check the
respective projects yourself if any of this needs to be current for your use case; dependency
lists in particular can change between major versions.

**"AOT-clean" isn't a checkbox here — it's how the library is built from day one.** Zero
reflection, zero `Activator.CreateInstance`, zero dynamic code generation anywhere in
`Sharp.Tui.Core`/`Layout`/`Widgets`/`Runtime`. The library is built to be NativeAOT-compatible;
end-to-end `PublishAot` verification, with measured binary sizes, is still in progress
(see [#30](https://github.com/Goshya/Sharp.Tui/issues/30)).

## Getting started

```csharp
// Program.cs
using Sharp.Tui.Core.Input;
using Sharp.Tui.Runtime;
using Sharp.Tui.Widgets;

Tui.Run(new CounterApp(), keyMap: key => key is { Code: KeyCode.Char, Modifiers: KeyModifiers.None }
    ? key.Char.Value switch
    {
        '+' or '=' => new Increment(),
        '-' => new Decrement(),
        'q' => new QuitRequested(),
        _ => (Msg?)null,
    }
    : null);

// The app's own message type — a plain record hierarchy, nothing library-defined.
internal abstract record Msg;
internal sealed record Increment : Msg;
internal sealed record Decrement : Msg;
internal sealed record QuitRequested : Msg;

internal sealed record Model(int Count);

internal sealed class CounterApp : IApp<Model, Msg>
{
    public (Model Model, Cmd<Msg> Cmd) Init() => (new Model(0), Cmd.None<Msg>());

    public (Model Model, Cmd<Msg> Cmd) Update(Model model, Msg msg) => msg switch
    {
        Increment => (model with { Count = model.Count + 1 }, Cmd.None<Msg>()),
        Decrement => (model with { Count = model.Count - 1 }, Cmd.None<Msg>()),
        QuitRequested => (model, Cmd.Quit<Msg>()), // quitting is an effect Update returns
        _ => (model, Cmd.None<Msg>()),
    };

    public Widget View(Model model) => new Text($"Count: {model.Count}  (+/- to change, q to quit)");
}
```

```bash
dotnet run --project samples/Counter
```

See `samples/` for more: `Stopwatch` (a `Sub`-driven ticking clock), `ItemBrowser`
(`Block`/`ListView`/`Row` composed together), and `GitLogViewer` (the showcase app above — a real
async `Cmd` running an external process, browsing this very repository's own commit history).

## Non-goals (v1)

Scope discipline is what keeps a solo project shippable — these are deliberately out of scope
for now, not forgotten:

- Rich text editing widgets (multi-line editor, syntax highlighting)
- Full terminfo database support — targeting "works great on modern terminal emulators," not
  universal legacy terminal support
- Accessibility/screen-reader support (a known gap, not something faked)
- Advanced mouse gestures beyond click/scroll

## Platform support

| Platform | Status |
|---|---|
| Windows | Supported |
| Linux | Supported |
| macOS | **Not supported yet** — raw terminal mode isn't implemented there (see [#31](https://github.com/Goshya/Sharp.Tui/issues/31)) |

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

## Status

Pre-v1. See `docs/SPEC.md` for the full technical spec and `docs/SPEC.md` §10 for the milestone
roadmap.

## License

MIT.
