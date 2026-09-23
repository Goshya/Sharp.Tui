using System;
using System.Collections.Generic;
using System.Text;

namespace Sharp.Tui.Core.Rendering;

// Pure comparison of two frames: no knowledge of ITerminalWriter or how a run gets emitted to
// it — just "which column ranges, in which rows, need to be repainted."
internal static class FrameDiff
{
    // Two changed spans in the same row separated by this many (or fewer) unchanged cells get
    // merged into one run — rewriting a handful of unchanged cells is cheaper than paying for a
    // second cursor move. Provisional; revisit once the M1.3 benchmark has real numbers.
    private const int MaxMergeGap = 4;

    // front is null on the first frame, or when the caller has invalidated it (e.g. after a
    // resize) — in both cases _front can't be trusted, so the whole back buffer is repainted.
    //
    // runs is caller-owned and reused across frames (DiffRenderer keeps one instance for its
    // whole lifetime) rather than allocated here every call — Compute clears it up front.
    public static void Compute(Buffer? front, Buffer back, List<DiffRun> runs)
    {
        runs.Clear();

        if (front is null || front.Width != back.Width || front.Height != back.Height)
        {
            FullInvalidation(back, runs);
            return;
        }

        for (var y = 0; y < back.Height; y++)
            ComputeRow(front, back, y, runs);
    }

    private static void FullInvalidation(Buffer back, List<DiffRun> runs)
    {
        for (var y = 0; y < back.Height; y++)
            runs.Add(new DiffRun(y, 0, back.Width - 1));
    }

    private static void ComputeRow(Buffer front, Buffer back, int y, List<DiffRun> runs)
    {
        var runStart = -1;
        var runEnd = -1;

        for (var x = 0; x < back.Width; x++)
        {
            if (back[x, y].Equals(front[x, y]))
                continue;

            if (runStart == -1)
                runStart = x;
            else if (x - runEnd - 1 > MaxMergeGap)
            {
                runs.Add(new DiffRun(y, runStart, runEnd));
                runStart = x;
            }

            runEnd = x;
        }

        if (runStart != -1)
            runs.Add(new DiffRun(y, runStart, runEnd));
    }
}
