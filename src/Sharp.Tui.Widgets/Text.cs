using System.Text;
using Sharp.Tui.Core.Rendering;
using Sharp.Tui.Layout;
using Buffer = Sharp.Tui.Core.Rendering.Buffer;
// The Style property below shadows the Style type name inside this record's own body (a member
// always wins over a type of the same simple name in scope) — this alias is the only way to
// still reach Style.Default from in here without a fully-qualified name at every call site.
using CoreStyle = Sharp.Tui.Core.Rendering.Style;

namespace Sharp.Tui.Widgets;

// Minimal on purpose: plain text, one buffer cell per Rune, '\n' starts a new line. Whatever
// doesn't fit in the area is clipped (no wrapping). Alignment, wrapping and wide-character
// handling are still M4's job — only Style (docs/SPEC.md §2.6) has landed so far.
public sealed record Text(string Content, Style? Style = null) : Widget
{
    public override void Render(Buffer buffer, Rect area)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        // Resolved once, not per character — a Style never changes mid-render, so there's
        // nothing to gain from redoing this on every Rune.
        var (foreground, background, flags) = (Style ?? CoreStyle.Default).Resolve();

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
                buffer[x, y] = new Cell(rune, foreground, background, flags);

            x++;
        }
    }

    public override Size Measure(Constraints constraints)
    {
        var (width, height) = MeasureLines(Content);

        return new Size(
            Math.Clamp(width, constraints.MinWidth, constraints.MaxWidth),
            Math.Clamp(height, constraints.MinHeight, constraints.MaxHeight));
    }

    // Deliberately mirrors Render's own line-splitting rune by rune, not a simpler-looking
    // Content.Split('\n') — that would count '\r' as part of the preceding line's length (wrong:
    // Render skips it) and count UTF-16 chars instead of Runes (wrong for anything outside the
    // BMP, e.g. an emoji, which Render draws as a single cell). Content is treated as one line
    // even when empty or null, same as Render's `Content ?? string.Empty`.
    private static (int Width, int Height) MeasureLines(string? content)
    {
        var width = 0;
        var height = 1;
        var lineWidth = 0;

        foreach (var rune in (content ?? string.Empty).EnumerateRunes())
        {
            if (rune.Value == '\n')
            {
                width = Math.Max(width, lineWidth);
                lineWidth = 0;
                height++;
                continue;
            }

            if (rune.Value == '\r')
                continue;

            lineWidth++;
        }

        width = Math.Max(width, lineWidth);
        return (width, height);
    }
}
