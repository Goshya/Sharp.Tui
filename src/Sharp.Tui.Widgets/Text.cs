using System.Text;
using Sharp.Tui.Core.Rendering;
using Sharp.Tui.Layout;
using Buffer = Sharp.Tui.Core.Rendering.Buffer;

namespace Sharp.Tui.Widgets;

// Minimal on purpose: plain unstyled text, one buffer cell per Rune, '\n' starts a new line.
// Whatever doesn't fit in the area is clipped (no wrapping). Styles, alignment, wrapping and
// wide-character handling are M4's job.
public sealed record Text(string Content) : Widget
{
    public override void Render(Buffer buffer, Rect area)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        // Clip to the buffer as well, so an oversized area can't make the indexer throw.
        var right = Math.Min(area.X + area.Width, buffer.Width);
        var bottom = Math.Min(area.Y + area.Height, buffer.Height);
        var x = area.X;
        var y = area.Y;

        foreach (var rune in (Content ?? string.Empty).EnumerateRunes())
        {
            if (rune.Value == '\n')
            {
                x = area.X;
                y++;
                continue;
            }

            if (rune.Value == '\r')
                continue;

            if (y >= bottom)
                return;

            if (x >= area.X && x < right && y >= area.Y)
                buffer[x, y] = new Cell(rune, Color.Default, Color.Default, StyleFlags.None);

            x++;
        }
    }
}
