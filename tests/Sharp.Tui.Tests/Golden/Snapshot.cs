using System.Runtime.CompilerServices;
using Sharp.Tui.Layout;
using Sharp.Tui.Widgets;
using Buffer = Sharp.Tui.Core.Rendering.Buffer;

namespace Sharp.Tui.Tests.Golden;

// Golden-file ("snapshot") testing for widgets (docs/SPEC.md §5) — the highest-leverage test type
// for anything that draws, since it catches layout/rendering regressions a unit test on one
// method in isolation would miss. Workflow: render a widget, compare the resulting text grid
// against a file committed to source control. A mismatch (or a missing golden file) never just
// fails silently — it writes the actual output next to the golden file as `<name>.received.txt`,
// so the fix is "open both files, decide which is right, and rename .received.txt over the
// golden file if the new output is correct" (the same workflow Rust's `insta` uses).
internal static class Snapshot
{
    // [CallerFilePath] on a parameterless call captures *this file's own* path at compile time,
    // regardless of where the test assembly actually runs from (bin/Debug/...) — so golden files
    // are always read from and written to the source tree, never a stale build-output copy.
    private static readonly string GoldenDirectory = GetThisDirectory();

    private static string GetThisDirectory([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;

    // Renders `widget` into a fresh width x height Buffer and serializes it to a plain text
    // grid: one line per row, one character per cell, no style annotation yet (added once a
    // widget that actually sets one needs it tested — expected to be Block, #19). Trailing blank
    // cells are kept, not trimmed: the full-frame render contract means blank space is part of
    // what's being checked, not incidental whitespace to throw away.
    public static string RenderToGrid(Widget widget, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(widget);

        var buffer = new Buffer(width, height);
        widget.Render(buffer, new Rect(0, 0, width, height));

        var lines = new string[height];
        for (var y = 0; y < height; y++)
        {
            var chars = new char[width];
            for (var x = 0; x < width; x++)
                chars[x] = (char)buffer[x, y].Char.Value;

            lines[y] = new string(chars);
        }

        return string.Join('\n', lines);
    }

    // Compares `actual` against the committed golden file `goldenFileName` (resolved relative to
    // this Golden/ directory). SHARP_TUI_UPDATE_GOLDEN=1 makes any mismatch overwrite the golden
    // file instead of failing — the escape hatch for "I changed rendering on purpose, regenerate
    // everything" instead of reviewing and renaming one file at a time.
    public static void AssertMatchesGoldenFile(string actual, string goldenFileName)
    {
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(goldenFileName);

        var goldenPath = Path.Combine(GoldenDirectory, goldenFileName);
        var receivedPath = goldenPath + ".received.txt";

        if (Environment.GetEnvironmentVariable("SHARP_TUI_UPDATE_GOLDEN") == "1")
        {
            File.WriteAllText(goldenPath, actual);
            DeleteIfExists(receivedPath);
            return;
        }

        if (!File.Exists(goldenPath))
        {
            File.WriteAllText(receivedPath, actual);
            Assert.Fail(
                $"Golden file '{goldenFileName}' does not exist yet. Wrote the actual output to " +
                $"'{receivedPath}' — review it, then rename it over '{goldenFileName}' (or rerun " +
                "with SHARP_TUI_UPDATE_GOLDEN=1) if it's correct.");
            return;
        }

        var expected = File.ReadAllText(goldenPath);
        if (expected == actual)
        {
            DeleteIfExists(receivedPath); // stale leftover from a previous failing run
            return;
        }

        File.WriteAllText(receivedPath, actual);
        Assert.Fail(
            $"Rendered output does not match golden file '{goldenFileName}'. Actual output " +
            $"written to '{receivedPath}' — compare it, then replace '{goldenFileName}' with it " +
            "(or rerun with SHARP_TUI_UPDATE_GOLDEN=1) if the change is intentional.");
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }
}
