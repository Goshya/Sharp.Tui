namespace Sharp.Tui.Layout;

// Integer cell coordinates only (docs/SPEC.md §2.4). Only what Widget.Render needs to know where
// to draw exists so far — Constraints/Size/measurement arrive with M3.
public readonly record struct Rect(int X, int Y, int Width, int Height);
