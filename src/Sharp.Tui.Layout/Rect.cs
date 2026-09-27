namespace Sharp.Tui.Layout;

// Integer cell coordinates only (docs/SPEC.md §2.4). Only what Widget.Render needs to know where
// to draw exists so far — Constraints/Size/measurement arrive with M3.
public readonly record struct Rect(int X, int Y, int Width, int Height)
{
    // The overlapping region of this rect and `other` — used by Row/Column/Grid to clip a
    // child's computed area to its parent before rendering (docs/SPEC.md §2.4). If the two
    // don't overlap at all, returns a zero-area Rect rather than throwing: an empty area is
    // itself a valid, harmless thing to hand to Widget.Render.
    public Rect Intersect(Rect other)
    {
        var x1 = Math.Max(X, other.X);
        var y1 = Math.Max(Y, other.Y);
        var x2 = Math.Min(X + Width, other.X + other.Width);
        var y2 = Math.Min(Y + Height, other.Y + other.Height);

        return new Rect(x1, y1, Math.Max(0, x2 - x1), Math.Max(0, y2 - y1));
    }
}
