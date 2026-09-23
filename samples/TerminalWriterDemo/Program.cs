using System.Text;
using Sharp.Tui.Core.Input;
using Sharp.Tui.Core.Rendering;
using Sharp.Tui.Core.Terminal;

// Manual smoke test for AnsiTerminalWriter + RawMode against a real stdout stream / real console
// mode — dotnet test only ever exercises these against a MemoryStream and fakes, so this is the
// actual-terminal check CLAUDE.md requires for anything touching Sharp.Tui.Core.
//
// Pass --crash to deliberately throw while raw mode is active, to manually verify M1.4's "Done
// when" requirement: the shell must be left in a normal, usable state even after an unhandled
// exception, not just on a clean exit.
//
// Pass --watch-resize to instead print ResizeEvents for 10 seconds — the SIGWINCH path (Linux/
// macOS/FreeBSD) can't be unit tested at all (it needs a real signal delivered to a real pty),
// so this is the only way to verify it actually fires and reports the right size.
//
// Pass --read-keys to instead enter raw mode and echo back each real keypress read through
// InputReader — the one thing none of the other modes prove: that raw mode's console/termios
// setup (M1.4) actually delivers bytes to InputParser (M1.5) the way it expects, end to end in
// a real terminal, not just each piece tested in isolation. This is M1's own "Done when":
// enter raw mode, draw, read a keypress, exit cleanly.

if (args.Contains("--read-keys"))
{
    var keyCapabilities = TerminalCapabilities.Detect();
    var keyWriter = new AnsiTerminalWriter(Console.OpenStandardOutput(), keyCapabilities.ColorSupport);
    using var keyRawMode = RawMode.Enter(keyWriter);

    var reader = new InputReader(Console.OpenStandardInput());
    await foreach (var evt in reader.ReadAsync())
    {
        keyWriter.Clear();
        keyWriter.MoveCursor(2, 1);
        WriteText(keyWriter, "Press keys (letters, arrows, Ctrl+X, ...) — Esc to exit.");
        keyWriter.MoveCursor(2, 3);
        WriteText(keyWriter, DescribeEvent(evt));
        keyWriter.Flush();

        if (evt.Kind == InputEventKind.Key && evt.AsKey.Code == KeyCode.Escape)
            break;
    }

    return;
}

if (args.Contains("--watch-resize"))
{
    Console.WriteLine($"Initial size: {Console.WindowWidth}x{Console.WindowHeight}");
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    try
    {
        await foreach (var evt in new ResizeWatcher().WatchAsync(cts.Token))
            Console.WriteLine($"Resize: {evt.AsResize}");
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Done watching (10s elapsed).");
    }
    return;
}

var shouldCrash = args.Contains("--crash");

var capabilities = TerminalCapabilities.Detect();
var writer = new AnsiTerminalWriter(Console.OpenStandardOutput(), capabilities.ColorSupport);

// RawMode.Enter now owns everything Program.cs used to wire up by hand in the M1.2 version of
// this demo (alternate screen, hidden cursor, ProcessExit/CancelKeyPress restore hooks) — plus
// the actual OS-level raw mode toggle, which is the part this demo exists to prove works.
using var rawMode = RawMode.Enter(writer);

writer.Clear();

writer.MoveCursor(2, 1);
writer.SetStyle(Color.Rgb(255, 215, 0), Color.Default, StyleFlags.Bold);
WriteText(writer, "Sharp.Tui — RawMode smoke test");
writer.SetStyle(Color.Default, Color.Default, StyleFlags.None);

writer.MoveCursor(2, 3);
WriteText(writer, $"Detected color support: {capabilities.ColorSupport}");

writer.MoveCursor(2, 5);
writer.SetStyle(Color.Named(1), Color.Default, StyleFlags.None);
WriteText(writer, "Named16   ");
writer.SetStyle(Color.Named(9), Color.Default, StyleFlags.Bold);
WriteText(writer, "(bright) ");
writer.SetStyle(Color.Default, Color.Default, StyleFlags.None);
WriteText(writer, "red / bright red");

writer.MoveCursor(2, 6);
writer.SetStyle(Color.Indexed(213), Color.Default, StyleFlags.None);
WriteText(writer, "Indexed256 pink");
writer.SetStyle(Color.Default, Color.Default, StyleFlags.None);

writer.MoveCursor(2, 7);
writer.SetStyle(Color.Rgb(64, 200, 255), Color.Default, StyleFlags.Italic | StyleFlags.Underline);
WriteText(writer, "TrueColor italic underline");
writer.SetStyle(Color.Default, Color.Default, StyleFlags.None);

writer.MoveCursor(2, 9);
WriteText(writer, shouldCrash
    ? "Raw mode is active. Throwing an unhandled exception in 2 seconds..."
    : "Raw mode is active — type anything, nothing should echo. Press Enter to exit.");
writer.Flush();

if (shouldCrash)
{
    Thread.Sleep(2000);
    throw new InvalidOperationException("Deliberate crash to verify RawModeScope restores the terminal even on an unhandled exception.");
}

Console.ReadLine();

static void WriteText(ITerminalWriter writer, string text)
{
    foreach (var rune in text.EnumerateRunes())
        writer.Write(rune);
}

static string DescribeEvent(InputEvent evt) => evt.Kind switch
{
    InputEventKind.Key => evt.AsKey.Code == KeyCode.Char
        ? $"Key: '{evt.AsKey.Char}'  Modifiers={evt.AsKey.Modifiers}"
        : $"Key: {evt.AsKey.Code}  Modifiers={evt.AsKey.Modifiers}",
    InputEventKind.Mouse => $"Mouse: {evt.AsMouse}",
    InputEventKind.Resize => $"Resize: {evt.AsResize}",
    InputEventKind.Paste => $"Paste: \"{evt.AsPaste}\"",
    _ => evt.ToString() ?? "",
};
