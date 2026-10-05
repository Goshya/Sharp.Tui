# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project aims to follow
[Semantic Versioning](https://semver.org/) once v1 ships. Before 1.0, minor versions may contain
breaking API changes — they are always listed here under **Changed** or **Removed**.

## [Unreleased]

### Added

- Terminal core: cell buffer, ANSI diff renderer, raw-mode terminal I/O on Windows and Linux,
  input parser (keys, SS3/CSI arrows).
- Layout engine: `Constraints`, `SizeMode` (`Fixed`/`Percent`/`Fill`/`Auto`), `LayoutSolver`,
  `Row`/`Column`/`Stack`/`Grid`.
- Widgets: `Text`, `Block`, `Gauge`, `ListView`, `Table`, plus `Theme`.
- TEA runtime: `IApp<TModel, TMsg>`, `Cmd<TMsg>`, `Sub<TMsg>`, `Tui.Run`.
- Samples: `Counter`, `Stopwatch`, `ItemBrowser`, `GitLogViewer`.
- NuGet packaging: a single `Sharp.Tui` package bundling the four assemblies, with no dependencies, SourceLink and embedded symbols (`eng/verify-package.sh` checks it end to end).

### Known limitations

- macOS is not supported yet: raw terminal mode is not implemented there, and `RawMode.Enter` throws a `PlatformNotSupportedException` with a pointer to [#35](https://github.com/Goshya/Sharp.Tui/issues/35) before touching the terminal.
