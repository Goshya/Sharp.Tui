# Contributing to Sharp.Tui

Thanks for your interest. Sharp.Tui is a small, opinionated library — please read this page and
[`docs/SPEC.md`](docs/SPEC.md) (the source of truth for design decisions) before sending a larger change.
For anything non-trivial, open an issue first so the approach can be agreed before you write code.

## Build and test

Requires the .NET 10 SDK.

```bash
dotnet build
dotnet test
dotnet run --project samples/Counter
```

A change isn't done until the build is clean (no warnings), the tests are green and — for anything
touching `Core` or `Layout` — a sample has been run in a real terminal. Layout and rendering bugs
routinely don't show up in unit tests alone.

## Ground rules

- **Zero runtime dependencies** in `Core`/`Layout`/`Widgets`/`Runtime`. Test and benchmark projects
  may use xUnit/BenchmarkDotNet. If you think a package is an obvious win, open an issue first —
  being dependency-free is a stated goal of the project.
- **NativeAOT/trim-clean.** No reflection, `Activator.CreateInstance` or dynamic code generation in the
  library projects.
- **Immutable state, pure functions.** Models, messages and styles are `record`/`readonly struct`.
  Widgets are pure functions of their props; mutable state (selection, scroll offset, cursor)
  belongs in the application's `Model`, not the widget.
- **Every new widget or layout primitive needs a golden-file test**: render to a buffer and compare
  against a committed text grid (see `tests/Sharp.Tui.Tests/Golden/`).
- **Always restore the terminal** (raw mode, alternate screen) on exit, including on exceptions and
  Ctrl+C.
- **Public API changes** must be called out in the PR and logged in [`CHANGELOG.md`](CHANGELOG.md).
- Style: file-scoped namespaces, one public type per file, PascalCase public members,
  `_camelCase` private fields, nullable reference types enabled.

## Performance

Diffing and flushing a 200×50 buffer should stay well under 2 ms. If a change to `Core` or `Layout`
could affect that, run the benchmarks in `benchmarks/`:

```bash
dotnet run -c Release --project benchmarks/Sharp.Tui.Benchmarks
```

## Where to look

- `docs/SPEC.md` — architecture, API sketch, testing strategy, platform notes, roadmap
- `samples/` — runnable examples
