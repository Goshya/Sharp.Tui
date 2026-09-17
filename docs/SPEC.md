# Sharp.Tui — Technical Specification (v0.1 draft)

## 1. Vision & positioning

**The gap.** In the .NET ecosystem, `Terminal.Gui` (~11k★) is mature and widget-rich but its API is dated — event-driven, class-hierarchy-based, closer to WinForms than to anything modern. `Spectre.Console` (~11k★) is excellent, but it's a *styled output* library (tables, panels, markup) — not a full interactive TUI runtime with input handling and app state. Spectre's own team started `spectre.tui` to close that gap, but it's stalled (~50★, "under construction," low activity). Meanwhile Go (Bubble Tea) and Rust (Ratatui) both have popular, modern TUI frameworks built on an immutable, functional model — and nothing in .NET matches that combination of ergonomics and polish.

**The bet.** Build a small, opinionated, dependency-free TUI framework around **The Elm Architecture (TEA)**: immutable `Model`, pure `Update`, pure `View`, side effects modeled explicitly as `Cmd`. This is a genuine architectural upgrade over the existing .NET options, not just a reskin, and it doubles as a showcase for modern C# (records, pattern matching, source generators).

**Non-goals for v1** (say so explicitly in the README — scope discipline is what keeps a solo project shippable):
- Rich text editing widgets (multi-line editor, syntax highlighting)
- Full terminfo database support — target "works great on modern terminal emulators" like Ratatui does, not universal legacy terminal support
- Accessibility/screen-reader support (acknowledge as a known gap, don't fake it)
- Advanced mouse gestures beyond click/scroll

## 2. Core architecture

```
 ┌─────────────┐   Msg    ┌─────────────┐   Model    ┌─────────────┐
 │  Input /    │ ───────▶ │   Update    │ ─────────▶ │    View     │
 │  Cmd results│          │  (pure fn)  │            │  (pure fn)  │
 └─────────────┘          └─────────────┘            └──────┬──────┘
        ▲                                                     │
        │                                              Widget tree
        │                                                     ▼
 ┌─────────────┐  diff + ANSI writes   ┌─────────────────────────┐
 │  Terminal   │ ◀──────────────────── │  Layout + Render buffer │
 │  (stdout)   │                       │   (double-buffered)     │
 └─────────────┘                       └─────────────────────────┘
```

### 2.1 Rendering pipeline

- `Cell = { Rune Char, Color Foreground, Color Background, StyleFlags Style }` — a fixed-size struct, no boxing.
- `Buffer` — a flat `Cell[Width * Height]` (or `Cell[,]`) with an indexer; represents one full frame.
- **Double buffering**: keep the previously-flushed buffer and the freshly-rendered one; diff cell-by-cell, batch contiguous same-row runs into a single ANSI cursor-move + write, and only touch cells that actually changed. This is the single biggest lever for making the app feel instant and for keeping CPU/flicker down on slow terminals (e.g. over SSH).
- `ITerminalWriter` abstraction over raw ANSI/VT sequence emission, so the diff algorithm is testable without a real terminal attached.

### 2.2 Input handling

- **Raw mode**: on Unix, P/Invoke `tcgetattr`/`tcsetattr` to disable canonical mode and echo; on Windows, `SetConsoleMode` with `ENABLE_VIRTUAL_TERMINAL_INPUT` and disabling line-input/echo flags. Wrap both behind one `IRawModeScope : IDisposable` so raw mode is always restored on exit/crash (register on `AppDomain.ProcessExit` and `Console.CancelKeyPress` too — leaving a user's terminal in raw mode on crash is an instant one-star review).
- An async reader loop parses raw stdin bytes into typed events: `KeyEvent`, `MouseEvent`, `ResizeEvent`, `PasteEvent` (bracketed paste, if in scope).
- **Resize detection**: `PosixSignalRegistration` for `SIGWINCH` on Unix; polling `Console.WindowWidth/Height` on Windows (no native resize signal there).

### 2.3 Application runtime (The Elm Architecture)

```csharp
public interface IApp<TModel, TMsg>
{
    (TModel Model, Cmd<TMsg> Cmd) Init();
    (TModel Model, Cmd<TMsg> Cmd) Update(TModel model, TMsg msg);
    Widget View(TModel model);
}

// A side effect that eventually produces zero or one message.
public delegate Task<TMsg?> Cmd<TMsg>(CancellationToken ct);

// A long-lived source of messages (timers, file watchers, subprocess output).
public delegate IAsyncEnumerable<TMsg> Sub<TMsg>(CancellationToken ct);
```

Runtime loop: an internal `Channel<TMsg>` merges keyboard/mouse/resize events, `Sub` output, and results from dispatched `Cmd`s. Each message goes through `Update`, producing a new `Model` (+ optional new `Cmd`), which is passed to `View` to produce a widget tree, which is measured, laid out, rendered into the back buffer, diffed, and flushed. Target: this whole cycle should comfortably fit inside a 16ms frame for typical apps.

### 2.4 Layout engine

- Flexbox-inspired, two-pass (measure, then arrange), integer cell coordinates only — no sub-pixel/fractional layout needed, which keeps this far simpler than a real UI layout engine.
- `Constraints { int MinWidth, MaxWidth, MinHeight, MaxHeight }`, `Rect { int X, Y, Width, Height }`.
- Sizing modes on children: `Fixed(n)`, `Percent(p)`, `Fill(weight)` (CSS `flex-grow`-style).
- Container primitives: `Row`, `Column`, `Stack` (z-order, for popups/modals/toasts), `Grid` (for table-like layouts).

### 2.5 Widget model

- `IWidget { Size Measure(Constraints c); void Render(Buffer buf, Rect area); }`.
- Widgets are **pure functions of their props** — records, not stateful classes. All mutable state (selected index, scroll offset, input cursor position) lives in the app's `Model`, exactly like React/Elm. This is the main API-level differentiator from `Terminal.Gui`'s stateful-view-with-events model, and it's what makes snapshot testing (§5) possible at all.
- v1 widget set (deliberately small — ship 10 polished widgets, not 50 mediocre ones):
  1. `Text` / `Paragraph` (wrapping, alignment)
  2. `Block` (bordered panel with optional title)
  3. `ListView` (selectable, scrollable)
  4. `Table`
  5. `Gauge` / `ProgressBar`
  6. `Sparkline`
  7. `TextInput` (single-line)
  8. `Tabs`
  9. `Popup` / `Modal`
  10. `ScrollView`

### 2.6 Styling

- `Style { Color? Foreground, Color? Background, bool Bold, Italic, Underline, ... }` — mergeable/overridable (child styles override parent, unset fields inherit).
- `Color`: named 16-color, 256-indexed, and `Rgb(r,g,b)` (TrueColor), with automatic downgrade based on detected terminal capability (`COLORTERM`, `TERM`, and a Windows Terminal check). Implement detection directly rather than depending on Spectre.Console — staying dependency-free is itself part of the pitch.
- `Theme` — a swappable record bundling default styles for built-in widgets.

### 2.7 Focus & key bindings

- Simple focus chain over the widget tree (Tab / Shift+Tab traversal order = tree order unless overridden).
- Per-widget-type default key bindings (e.g. arrows in `ListView`) plus an app-level global key map the user's `Update` function checks first.

## 3. Example API sketch

```csharp
// The canonical "counter" example — the TUI equivalent of a Hello World,
// and worth having pixel-perfect in the README on day one.

record Model(int Count);
record Increment : IMsg;
record Decrement : IMsg;

class CounterApp : IApp<Model, IMsg>
{
    public (Model, Cmd<IMsg>) Init() => (new Model(0), Cmd<IMsg>.None);

    public (Model, Cmd<IMsg>) Update(Model m, IMsg msg) => msg switch
    {
        Increment => (m with { Count = m.Count + 1 }, Cmd<IMsg>.None),
        Decrement => (m with { Count = m.Count - 1 }, Cmd<IMsg>.None),
        _ => (m, Cmd<IMsg>.None)
    };

    public Widget View(Model m) =>
        new Block(title: "counter")
        {
            Child = new Column
            {
                new Text($"Count: {m.Count}"),
                new Text("[+] increment  [-] decrement  [q] quit")
            }
        };
}

Program.Run(new CounterApp(), keyMap: key => key switch
{
    { Char: '+' } => new Increment(),
    { Char: '-' } => new Decrement(),
    { Char: 'q' } => Program.Quit,
    _ => null
});
```

A second, "showcase" sample (see §10, M5) should demonstrate `Cmd`/`Sub` with real async work — e.g. a `docker ps`-polling dashboard or a `git log` viewer — since that's the kind of demo that makes a good README GIF.

## 4. Project structure

```
Sharp.Tui.sln
src/
  Sharp.Tui.Core/         buffer, ANSI diff renderer, raw-mode terminal I/O, input parsing
  Sharp.Tui.Layout/       layout engine (Row/Column/Stack/Grid, sizing)
  Sharp.Tui.Widgets/      built-in widgets
  Sharp.Tui.Runtime/      TEA runtime: Program, Cmd<T>, Sub<T>
samples/
  Counter/
  ProcessMonitor/
  GitLogViewer/           the "wow" showcase demo for the README GIF
tests/
  Sharp.Tui.Tests/        unit + snapshot/golden-file tests
benchmarks/
  Sharp.Tui.Benchmarks/   BenchmarkDotNet render-loop perf
docs/
  SPEC.md                 this file
  adr/                    architecture decision records, one file per notable decision
```

## 5. Testing strategy

- **Snapshot/golden-file tests**: render a `Model` through `View` → layout → buffer, serialize the buffer as a plain text grid (plus a compact style annotation), and compare against a committed golden file — the same technique Ratatui uses via Rust's `insta`. This is the highest-leverage test type here: it catches layout and rendering regressions that unit tests on individual functions would miss.
- Unit tests for the layout engine: given `Constraints` + child sizing modes, assert the resulting `Rect`s.
- Unit tests for the ANSI diff writer: given two buffers, assert the minimal escape-sequence output (this also documents the escape-sequence format).
- Manual/exploratory pass across Windows Terminal, iTerm2, GNOME Terminal/Alacritty, and inside `tmux`/`screen` before each release — automated tests can't catch everything terminal-related.

## 6. Performance considerations

- No per-frame heap allocations on the hot path: pool/reuse buffer arrays, build escape sequences via `IBufferWriter<byte>`/`stackalloc` rather than string concatenation.
- Target budget: diffing and flushing a 200×50 buffer in well under 2ms on typical hardware.
- NativeAOT- and trim-compatible from day one — no reflection-based DI, no `Activator.CreateInstance`, no dynamic code generation anywhere in `Core`/`Layout`/`Widgets`/`Runtime`. This is worth stating loudly in the README: "AOT-clean" is a real differentiator right now.

## 7. Cross-platform notes

- **Windows**: legacy `conhost` vs. Windows Terminal behave differently; require VT processing to be enabled (`ENABLE_VIRTUAL_TERMINAL_PROCESSING`) and document a minimum of Windows Terminal + PowerShell 7 as the supported baseline rather than chasing `cmd.exe` edge cases.
- **Unix**: don't attempt full `terminfo` compatibility; target "correct on modern terminal emulators," matching the scope Ratatui and Bubble Tea both settled on.
- Everything is stdin/stdout stream-based, so SSH sessions should work without special-casing.

## 8. Packaging & distribution

- Ship as a single NuGet package (`Sharp.Tui`) for v1 rather than splitting `Core`/`Layout`/`Widgets`/`Runtime` into separate packages — simpler for adopters; split later only if a real need (e.g. someone wanting just the renderer) shows up.
- `net10.0` target (current LTS, supported into ~2028) — single-target for v1, add a newer TFM only when there's a concrete reason, not speculatively.
- MIT license (matches ecosystem norms for `Terminal.Gui`/`Spectre.Console`, minimizes adoption friction), SourceLink, deterministic builds, SemVer from `v0.1.0`.

## 9. Path to stars (marketing plan)

- README hero: a looping GIF (recorded with VHS or terminalizer) of the showcase app, visible in under 10 seconds of scrolling.
- A comparison table vs. `Terminal.Gui` and `Spectre.Console` near the top of the README — visitors from those repos' "alternatives" searches are a real acquisition channel.
- Launch posts: r/dotnet, r/csharp, Hacker News "Show HN," submit a PR to `awesome-dotnet` and `awesome-tuis`.
- Tag `v0.1.0` early, even minimal — a project with tags, a CI badge, and a working sample gets taken more seriously than a perfect but unreleased one.

## 10. Milestones

| # | Milestone | Deliverable / "done" signal | Rough effort |
|---|---|---|---|
| M0 | Repo scaffold | Solution + projects above, CI matrix (Windows/Linux/macOS build+test), LICENSE, CONTRIBUTING.md | 1–2 days |
| M1 | Core renderer | Static screen draws correctly, raw-mode keypress read back, clean exit restores terminal | 1 week |
| M2 | TEA runtime | `Program.Run`, `Cmd`/`Sub` plumbing, a tick subscription working end-to-end | 3–5 days |
| M3 | Layout engine | `Row`/`Column`/`Stack`, `Fixed`/`Percent`/`Fill` sizing, unit-tested | 1 week |
| M4 | Core widgets | `Text`, `Block`, `ListView`, `Table`, `Gauge` implemented + snapshot-tested | 1–1.5 weeks |
| M5 | Showcase app | Polished demo app + README GIF + comparison table | 3–5 days |
| M6 | v0.1.0 release | NuGet package published, launch posts sent | — |

Work in this order — widgets (M4) depend on a stable layout API (M3), which depends on the runtime shape (M2). Jumping ahead to widgets before the runtime settles is the most likely way this stalls, the same way `spectre.tui` appears to have.

## 11. Decisions log

- **Project/package name: `Sharp.Tui`** (decided — replaces the earlier `SharpTui` placeholder used during naming brainstorm).
- **Target framework: `net10.0`** (decided — current LTS; single-target for v1, no speculative multi-targeting).
- Confirmed: zero runtime NuGet dependencies in `Core`/`Layout`/`Widgets` — reimplement terminal-capability detection rather than depending on `Spectre.Console`.
