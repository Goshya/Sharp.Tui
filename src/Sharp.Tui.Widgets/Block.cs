using System.Text;
using Sharp.Tui.Core.Rendering;
using Sharp.Tui.Layout;
using Buffer = Sharp.Tui.Core.Rendering.Buffer;

namespace Sharp.Tui.Widgets;

// A bordered panel wrapping one child widget, single-line box-drawing only (docs/SPEC.md §2.5) —
// the first widget that composes another widget rather than drawing content itself, and the
// first real consumer of Style/Theme (#18). An optional Title renders left-aligned in the top
// border, padded by one space on each side (e.g. "┌ Title ────┐"), truncated without an ellipsis
// if it doesn't fit — the same "clip, don't wrap or elide" rule Text already follows.
public sealed record Block : Widget
{
    public Widget Child { get; }
    public string? Title { get; }
    public Style? BorderStyle { get; }
    public Style? TitleStyle { get; }

    public Block(Widget child, string? title = null, Style? borderStyle = null, Style? titleStyle = null)
    {
        ArgumentNullException.ThrowIfNull(child);

        Child = child;
        Title = title;
        BorderStyle = borderStyle;
        TitleStyle = titleStyle;
    }

    public override void Render(Buffer buffer, Rect area)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        // Too small to draw a border at all (not even the four corners) — draw nothing rather
        // than a mangled partial frame or a negative-sized child area.
        if (area.Width < 2 || area.Height < 2)
            return;

        var (borderFg, borderBg, borderFlags) = (BorderStyle ?? Style.Default).Resolve();

        var left = area.X;
        var right = area.X + area.Width - 1;
        var top = area.Y;
        var bottom = area.Y + area.Height - 1;

        void SetBorderCell(int x, int y, char ch) =>
            buffer[x, y] = new Cell(new Rune(ch), borderFg, borderBg, borderFlags);

        SetBorderCell(left, top, '┌');
        SetBorderCell(right, top, '┐');
        SetBorderCell(left, bottom, '└');
        SetBorderCell(right, bottom, '┘');

        for (var x = left + 1; x < right; x++)
        {
            SetBorderCell(x, top, '─');
            SetBorderCell(x, bottom, '─');
        }

        for (var y = top + 1; y < bottom; y++)
        {
            SetBorderCell(left, y, '│');
            SetBorderCell(right, y, '│');
        }

        // Overwrites some of the '─' fill just drawn — that's the point, the title lives *in*
        // the top border, not above or below it. area.Width == 2 means left+1 == right, i.e. no
        // interior cell exists for a title to occupy, so there's nothing to do in that case.
        if (!string.IsNullOrEmpty(Title) && area.Width > 2)
        {
            var (titleFg, titleBg, titleFlags) = (TitleStyle ?? Style.Default).Resolve();
            var maxWidth = area.Width - 2; // cells strictly between the two top corners
            var x = left + 1;

            foreach (var rune in $" {Title} ".EnumerateRunes())
            {
                if (x - (left + 1) >= maxWidth)
                    break;

                buffer[x, top] = new Cell(rune, titleFg, titleBg, titleFlags);
                x++;
            }
        }

        var inner = new Rect(left + 1, top + 1, area.Width - 2, area.Height - 2).Intersect(area);
        Child.Render(buffer, inner);
    }

    // The child is asked with a Constraints shrunk by 2 on each axis first — not the Constraints
    // Block itself received — so it reports an honest size for the room it will actually get,
    // not one computed as if the border didn't exist. The border's own 2 cells are added back
    // afterward, and only the final total is clamped into Block's own constraints.
    public override Size Measure(Constraints constraints)
    {
        var innerConstraints = new Constraints(
            Math.Max(0, constraints.MinWidth - 2),
            Math.Max(0, constraints.MaxWidth - 2),
            Math.Max(0, constraints.MinHeight - 2),
            Math.Max(0, constraints.MaxHeight - 2));

        var childSize = Child.Measure(innerConstraints);

        return new Size(
            Math.Clamp(childSize.Width + 2, constraints.MinWidth, constraints.MaxWidth),
            Math.Clamp(childSize.Height + 2, constraints.MinHeight, constraints.MaxHeight));
    }
}
