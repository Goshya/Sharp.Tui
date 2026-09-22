using System.Text;
using Sharp.Tui.Core.Rendering;
using Sharp.Tui.Core.Terminal;

// Manual smoke test for AnsiTerminalWriter against a real stdout stream (M1.2's "Done when" clause) —
// dotnet test only ever exercises it against a MemoryStream, so this is the actual terminal check
// CLAUDE.md requires for anything touching Sharp.Tui.Core.

var capabilities = TerminalCapabilities.Detect();
var writer = new AnsiTerminalWriter(Console.OpenStandardOutput(), capabilities.ColorSupport);

Console.CancelKeyPress += (_, _) => Restore(writer);
AppDomain.CurrentDomain.ProcessExit += (_, _) => Restore(writer);

try
{
    writer.EnterAlternateScreen();
    writer.Clear();
    writer.HideCursor();

    writer.MoveCursor(2, 1);
    writer.SetStyle(Color.Rgb(255, 215, 0), Color.Default, StyleFlags.Bold);
    WriteText(writer, "Sharp.Tui — AnsiTerminalWriter smoke test");
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
    WriteText(writer, "Press Enter to exit...");
    writer.Flush();

    Console.ReadLine();
}
finally
{
    Restore(writer);
}

static void WriteText(ITerminalWriter writer, string text)
{
    foreach (var rune in text.EnumerateRunes())
        writer.Write(rune);
}

static void Restore(ITerminalWriter writer)
{
    writer.SetStyle(Color.Default, Color.Default, StyleFlags.None);
    writer.ShowCursor();
    writer.ExitAlternateScreen();
    writer.Flush();
}
