namespace Sharp.Tui.Core.Rendering;

// A possibly-incomplete style, meant to be layered: an unset (null) field means "inherit from
// whatever this is merged onto," not "off" — Bold: false and Bold: null are different inputs to
// Merge even though they resolve to the same StyleFlags bit once merging is done (docs/SPEC.md
// §2.6). Lives here, next to Cell/Color/StyleFlags, since Resolve's whole job is producing
// exactly what a Cell needs.
public readonly record struct Style(
    Color? Foreground = null,
    Color? Background = null,
    bool? Bold = null,
    bool? Italic = null,
    bool? Underline = null,
    bool? Strikethrough = null)
{
    // All fields unset — same idiom as Color.Default (Color.cs): every field being nullable
    // already makes `default` the "nothing specified" value, nothing to construct.
    public static readonly Style Default = default;

    // Every field this Style itself sets wins; anything left unset falls through to `parent`'s.
    // Chainable for more than two levels: `a.Merge(b).Merge(c)`.
    public Style Merge(Style parent) => new(
        Foreground ?? parent.Foreground,
        Background ?? parent.Background,
        Bold ?? parent.Bold,
        Italic ?? parent.Italic,
        Underline ?? parent.Underline,
        Strikethrough ?? parent.Strikethrough);

    // Turns a (possibly still-incomplete) Style into the concrete values Cell actually stores.
    // Unset colors become Color.Default; unset or false flag fields simply don't set their bit —
    // the null/false distinction only matters to Merge, not here.
    public (Color Foreground, Color Background, StyleFlags Flags) Resolve()
    {
        var flags = StyleFlags.None;
        if (Bold == true) flags |= StyleFlags.Bold;
        if (Italic == true) flags |= StyleFlags.Italic;
        if (Underline == true) flags |= StyleFlags.Underline;
        if (Strikethrough == true) flags |= StyleFlags.Strikethrough;

        return (Foreground ?? Color.Default, Background ?? Color.Default, flags);
    }
}
