# CLAUDE.md

Project context for Claude Code when working on **Sharp.Tui** — a modern, dependency-free TUI (terminal UI) framework for .NET built around The Elm Architecture (Model–Update–View).

Full technical spec: `docs/SPEC.md`. Read it before any architectural decision — this file is the day-to-day quick-reference, not a replacement for it.

## Project goal

Fill a real gap in the .NET ecosystem: `Terminal.Gui` is feature-rich but has a dated, WinForms-style API; `Spectre.Console` is excellent for styled *output* but isn't a full interactive TUI runtime; Spectre's own `spectre.tui` experiment aimed at that gap has stalled. The bet here is a small, modern, Bubble Tea/Ratatui-style library for C#: immutable state, pure `Update`/`View` functions, explicit `Cmd`-based side effects, zero runtime dependencies, NativeAOT-clean from day one.

**Status:** pre-MVP. Follow the milestone order in `docs/SPEC.md` §10 (M0 → M6). Do not start on widgets (M4) before the TEA runtime (M2) and layout engine (M3) are stable — everything downstream depends on their shape, and building widgets against a moving runtime API is the most likely way this stalls.

## Tech stack & conventions

- .NET 10 (`net10.0`, current LTS). Don't add multi-targeting speculatively — only when a concrete need appears.
- C# latest language version, `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>` in every project.
- **No reflection, `Activator.CreateInstance`, or dynamic codegen** anywhere in `Sharp.Tui.Core` / `Layout` / `Widgets` / `Runtime` — trim- and NativeAOT-compatibility is a stated differentiator, not an afterthought. Flag anything that risks it instead of adding it silently.
- Model/Msg/Style/Rect/Constraints are `record`/`readonly struct` — state is immutable. Widgets are pure functions of their props (`IWidget.Measure`/`Render`), not stateful classes with mutable fields; any mutable state (selection index, scroll offset, cursor position) belongs in the app's `Model`, not the widget.
- **Zero external NuGet dependencies** in `Core`/`Layout`/`Widgets`/`Runtime`. Test and benchmark projects may use xUnit/BenchmarkDotNet. Before adding any package to a non-test project — including something that looks like an obvious win, e.g. depending on `Spectre.Console` for color-capability detection — stop and ask; this is a stated pitch of the project, not an implementation detail to trade away for convenience.
- File-scoped namespaces, one public type per file, standard .NET naming (PascalCase public members, `_camelCase` private fields).

## Repository layout

```
src/
  Sharp.Tui.Core/      buffer, ANSI diff renderer, raw-mode terminal I/O, input parsing
  Sharp.Tui.Layout/    layout engine (Row/Column/Stack/Grid, sizing modes)
  Sharp.Tui.Widgets/   built-in widgets (Text, Block, ListView, Table, Gauge, ...)
  Sharp.Tui.Runtime/   TEA runtime: Program, Cmd<TMsg>, Sub<TMsg>
samples/               runnable examples, including the showcase demo used in the README
tests/                 xUnit + snapshot/golden-file tests
benchmarks/            BenchmarkDotNet render-loop performance tests
docs/SPEC.md           full technical spec — read before big architectural changes
docs/adr/              architecture decision records, one file per notable decision
```

This layout doesn't exist yet until M0 lands — create it as described the first time you touch the repo, rather than improvising a different structure.

## Build & test

```
dotnet build
dotnet test
dotnet run --project samples/Counter
```

A change isn't done until: `dotnet build` is clean, `dotnet test` is green, and — for anything touching `Core`/`Layout` — the relevant sample has been run in an actual terminal. Layout and rendering bugs routinely don't show up in unit tests alone; a quick manual pass in a real terminal is part of the workflow, not optional polish.

## Working conventions

- Follow the milestone order in `docs/SPEC.md` §10. If asked to jump ahead (e.g. "add a new widget" before the layout engine is done), say so and suggest sequencing it after the current milestone rather than silently complying.
- Any new widget or layout primitive needs a snapshot test (render to a buffer, compare against a committed golden text-grid file — see `tests/` for the pattern once M1/M4 land).
- Public API in `Sharp.Tui.Core`/`Runtime`/`Widgets` is the highest-risk surface for an early library trying to attract adopters. Flag breaking changes explicitly rather than making them silently; log them in `CHANGELOG.md` (create it at M0 if missing).
- Performance budget: diffing and flushing a 200×50 buffer should stay well under 2ms. If a change to `Core`/`Layout` risks that, add or run a benchmark in `benchmarks/` rather than guessing.
- Always restore terminal state (raw mode, alternate screen buffer) on exit, including on unhandled exceptions and Ctrl+C — a crash that leaves the user's shell in a broken state is disqualifying for a TUI library, not a minor bug.

## Where to look for design rationale

`docs/SPEC.md` covers architecture (§2), the API sketch (§3), testing strategy (§5), performance notes (§6), and cross-platform notes (§7). If something in this file and `SPEC.md` ever disagree, `SPEC.md` is the source of truth — update this file to match it, not the other way around.
