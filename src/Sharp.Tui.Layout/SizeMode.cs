namespace Sharp.Tui.Layout;

// How a container child is sized along one axis (docs/SPEC.md §2.4): a fixed cell count, a
// percentage of the container's own size, a share of whatever space is left proportional to
// weight (CSS flex-grow-style), or sized to the child's own content (Widget.Measure). A closed
// set on purpose — LayoutSolver pattern-matches on the concrete cases below, so a fourth,
// externally-written implementation wouldn't participate in solving, only be silently ignored by
// it. `private protected` keeps the hierarchy closed to this assembly (mirrors Cmd/Sub's
// cached-instance pattern in Sharp.Tui.Runtime): callers only ever see SizeMode itself,
// constructed through the factories below.
public abstract record SizeMode
{
    private protected SizeMode() { }

    public static SizeMode Fixed(int cells) => new FixedSizeMode(cells);
    public static SizeMode Percent(double percent) => new PercentSizeMode(percent);
    public static SizeMode Fill(int weight) => new FillSizeMode(weight);

    // No parameters, unlike the other three, but still a method rather than a property — for
    // consistency with Fixed/Percent/Fill. Backed by a single cached instance regardless (there's
    // nothing to distinguish one Auto from another).
    private static readonly SizeMode AutoInstance = new AutoSizeMode();
    public static SizeMode Auto() => AutoInstance;
}

internal sealed record FixedSizeMode : SizeMode
{
    public int Cells { get; }

    public FixedSizeMode(int cells)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cells);
        Cells = cells;
    }
}

internal sealed record PercentSizeMode : SizeMode
{
    // Named Percentage, not Percent, to avoid hiding the inherited static SizeMode.Percent(double)
    // factory (CS0108) — the two aren't related overloads, just an unfortunate name clash.
    public double Percentage { get; }

    public PercentSizeMode(double percentage)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(percentage);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(percentage, 100);
        Percentage = percentage;
    }
}

internal sealed record FillSizeMode : SizeMode
{
    public int Weight { get; }

    public FillSizeMode(int weight)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(weight);
        Weight = weight;
    }
}

// No data of its own — LayoutSolver recognizes the type, not any property on it, and asks the
// caller-supplied measureAuto callback for the actual size.
internal sealed record AutoSizeMode : SizeMode;
