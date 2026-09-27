namespace Sharp.Tui.Layout;

// The math behind Row/Column/Grid (Sharp.Tui.Widgets): given the space available along one axis
// and each child's SizeMode, produce each child's size along that axis. Deliberately doesn't know
// about Widget or Rect — only about cell counts — so it's testable without a single widget or
// terminal (docs/SPEC.md §2.4). Row/Column call this once per Render, along their own axis; Grid
// calls it once per axis, once for its row tracks and once for its column tracks.
public static class LayoutSolver
{
    // Fixed sizes are taken first, in list order, then Percent — of `available` itself, not of
    // whatever Fixed left over — also in list order. Both draw from the same budget and are
    // first-come-first-served: once it hits zero, every later entry (Fixed, Percent, or Fill)
    // gets 0 rather than a negative size. Whatever the budget has left over after that is split
    // across the Fill entries proportionally by weight.
    public static int[] Solve(int available, IReadOnlyList<SizeMode> modes)
    {
        ArgumentNullException.ThrowIfNull(modes);
        ArgumentOutOfRangeException.ThrowIfNegative(available);

        var sizes = new int[modes.Count];
        var remaining = available;

        for (var i = 0; i < modes.Count; i++)
        {
            if (modes[i] is not FixedSizeMode fixedMode)
                continue;

            var size = Math.Min(fixedMode.Cells, remaining);
            sizes[i] = size;
            remaining -= size;
        }

        for (var i = 0; i < modes.Count; i++)
        {
            if (modes[i] is not PercentSizeMode percentMode)
                continue;

            var ideal = (int)Math.Round(available * percentMode.Percentage / 100.0, MidpointRounding.AwayFromZero);
            var size = Math.Min(ideal, remaining);
            sizes[i] = size;
            remaining -= size;
        }

        SolveFill(modes, remaining, sizes);
        return sizes;
    }

    // Splits `remaining` across the Fill entries proportionally by weight, using a
    // largest-remainder distribution: the base share is each entry's ideal size rounded down,
    // and whatever cells that rounding lost are handed out one at a time — largest fractional
    // remainder first, ties keeping list order — until none are left. Since `remaining` is
    // exactly what Fixed/Percent didn't take, the Fill entries' sizes always sum to precisely
    // `remaining`: no cell is ever lost to, or invented by, rounding.
    private static void SolveFill(IReadOnlyList<SizeMode> modes, int remaining, int[] sizes)
    {
        var fillIndexes = new List<int>();
        var totalWeight = 0;

        for (var i = 0; i < modes.Count; i++)
        {
            if (modes[i] is FillSizeMode { Weight: > 0 } fillMode)
            {
                fillIndexes.Add(i);
                totalWeight += fillMode.Weight;
            }
        }

        if (fillIndexes.Count == 0 || remaining <= 0)
            return;

        var remainders = new double[fillIndexes.Count];
        var assigned = 0;

        for (var i = 0; i < fillIndexes.Count; i++)
        {
            var weight = ((FillSizeMode)modes[fillIndexes[i]]).Weight;
            var ideal = remaining * (double)weight / totalWeight;
            var baseSize = (int)ideal;
            sizes[fillIndexes[i]] = baseSize;
            remainders[i] = ideal - baseSize;
            assigned += baseSize;
        }

        var leftover = Math.Clamp(remaining - assigned, 0, fillIndexes.Count);
        for (var round = 0; round < leftover; round++)
        {
            var bestIndex = 0;
            for (var i = 1; i < remainders.Length; i++)
            {
                if (remainders[i] > remainders[bestIndex])
                    bestIndex = i;
            }

            sizes[fillIndexes[bestIndex]]++;
            remainders[bestIndex] = -1; // consumed — never picked again
        }
    }
}
