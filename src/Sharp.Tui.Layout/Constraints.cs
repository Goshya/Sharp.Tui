namespace Sharp.Tui.Layout;

// The range a measured Size must fall into (docs/SPEC.md §2.4) — the input side of the two-pass
// "measure, then arrange" algorithm. Not a positional record: Min <= Max is an invariant, not
// just a naming convention, so it's checked once here instead of by every caller.
public readonly record struct Constraints
{
    public int MinWidth { get; }
    public int MaxWidth { get; }
    public int MinHeight { get; }
    public int MaxHeight { get; }

    public Constraints(int minWidth, int maxWidth, int minHeight, int maxHeight)
    {
        if (minWidth > maxWidth)
            throw new ArgumentException($"minWidth ({minWidth}) must be <= maxWidth ({maxWidth}).", nameof(minWidth));
        if (minHeight > maxHeight)
            throw new ArgumentException($"minHeight ({minHeight}) must be <= maxHeight ({maxHeight}).", nameof(minHeight));

        MinWidth = minWidth;
        MaxWidth = maxWidth;
        MinHeight = minHeight;
        MaxHeight = maxHeight;
    }

    // "Be exactly this size" — both ends pinned to the same value.
    public static Constraints Tight(Size size) => new(size.Width, size.Width, size.Height, size.Height);

    // "Be no bigger than this" — free to be smaller, down to zero.
    public static Constraints Loose(Size size) => new(0, size.Width, 0, size.Height);
}
