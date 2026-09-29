using Sharp.Tui.Core.Rendering;

namespace Sharp.Tui.Widgets;

// A swappable bundle of default styles for built-in widgets (docs/SPEC.md §2.6) — a plain value,
// not something the runtime injects: IApp<TModel, TMsg>.View's signature doesn't change for this.
// An app picks a Theme by holding a reference to one (its own field, or Model) and passing the
// relevant Style into the widgets it builds in View, e.g. `new Block(..., BorderStyle:
// theme.Border)`. Border/Title/Selected are named ahead of the widgets that use them (Block in
// #19, ListView in #21) so those subtasks don't need to touch this contract again.
public sealed record Theme(Style Border, Style Title, Style Selected)
{
    public static readonly Theme Default = new(
        Border: new Style(),
        Title: new Style(Bold: true),
        Selected: new Style(Background: Color.Named(4)));
}
