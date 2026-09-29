using Sharp.Tui.Core.Rendering;

namespace Sharp.Tui.Tests.Rendering;

public class StyleTests
{
    [Fact]
    public void Default_IsAllUnset()
    {
        Assert.Equal(new Style(), Style.Default);
    }

    // --- Merge -------------------------------------------------------------------------------
    // Each field independently: the child wins when set, falls through to the parent when not.

    [Fact]
    public void Merge_ForegroundSetOnChild_ChildWins()
    {
        var child = new Style(Foreground: Color.Named(1));
        var parent = new Style(Foreground: Color.Named(2));

        Assert.Equal(Color.Named(1), child.Merge(parent).Foreground);
    }

    [Fact]
    public void Merge_ForegroundUnsetOnChild_FallsThroughToParent()
    {
        var child = new Style();
        var parent = new Style(Foreground: Color.Named(2));

        Assert.Equal(Color.Named(2), child.Merge(parent).Foreground);
    }

    [Fact]
    public void Merge_BackgroundSetOnChild_ChildWins()
    {
        var child = new Style(Background: Color.Named(1));
        var parent = new Style(Background: Color.Named(2));

        Assert.Equal(Color.Named(1), child.Merge(parent).Background);
    }

    [Fact]
    public void Merge_BackgroundUnsetOnChild_FallsThroughToParent()
    {
        var child = new Style();
        var parent = new Style(Background: Color.Named(2));

        Assert.Equal(Color.Named(2), child.Merge(parent).Background);
    }

    [Fact]
    public void Merge_BoldSetOnChild_ChildWins()
    {
        // Explicit false on the child must beat an inherited true — this is exactly why Bold is
        // bool? and not bool: null and false are different inputs to Merge.
        var child = new Style(Bold: false);
        var parent = new Style(Bold: true);

        Assert.Equal(false, child.Merge(parent).Bold);
    }

    [Fact]
    public void Merge_BoldUnsetOnChild_FallsThroughToParent()
    {
        var child = new Style();
        var parent = new Style(Bold: true);

        Assert.Equal(true, child.Merge(parent).Bold);
    }

    [Fact]
    public void Merge_ItalicSetOnChild_ChildWins()
    {
        var child = new Style(Italic: false);
        var parent = new Style(Italic: true);

        Assert.Equal(false, child.Merge(parent).Italic);
    }

    [Fact]
    public void Merge_ItalicUnsetOnChild_FallsThroughToParent()
    {
        var child = new Style();
        var parent = new Style(Italic: true);

        Assert.Equal(true, child.Merge(parent).Italic);
    }

    [Fact]
    public void Merge_UnderlineSetOnChild_ChildWins()
    {
        var child = new Style(Underline: false);
        var parent = new Style(Underline: true);

        Assert.Equal(false, child.Merge(parent).Underline);
    }

    [Fact]
    public void Merge_UnderlineUnsetOnChild_FallsThroughToParent()
    {
        var child = new Style();
        var parent = new Style(Underline: true);

        Assert.Equal(true, child.Merge(parent).Underline);
    }

    [Fact]
    public void Merge_StrikethroughSetOnChild_ChildWins()
    {
        var child = new Style(Strikethrough: false);
        var parent = new Style(Strikethrough: true);

        Assert.Equal(false, child.Merge(parent).Strikethrough);
    }

    [Fact]
    public void Merge_StrikethroughUnsetOnChild_FallsThroughToParent()
    {
        var child = new Style();
        var parent = new Style(Strikethrough: true);

        Assert.Equal(true, child.Merge(parent).Strikethrough);
    }

    [Fact]
    public void Merge_AllFieldsSetOnChild_ParentIsIgnoredEntirely()
    {
        var child = new Style(Color.Named(1), Color.Named(2), true, true, true, true);
        var parent = new Style(Color.Named(3), Color.Named(4), false, false, false, false);

        Assert.Equal(child, child.Merge(parent));
    }

    [Fact]
    public void Merge_AllFieldsUnsetOnChild_ResultEqualsParent()
    {
        var parent = new Style(Color.Named(3), Color.Named(4), true, false, true, false);

        Assert.Equal(parent, Style.Default.Merge(parent));
    }

    [Fact]
    public void Merge_Chained_ThreeLevels_NearestSetWinsPerField()
    {
        var grandparent = new Style(Foreground: Color.Named(1), Bold: true);
        var parent = new Style(Background: Color.Named(2)); // Foreground/Bold left unset
        var child = new Style(Bold: false); // overrides grandparent's Bold, leaves the rest unset

        var merged = child.Merge(parent).Merge(grandparent);

        Assert.Equal(Color.Named(1), merged.Foreground); // from grandparent
        Assert.Equal(Color.Named(2), merged.Background); // from parent
        Assert.Equal(false, merged.Bold); // from child, wins over grandparent's true
    }

    // --- Resolve -----------------------------------------------------------------------------

    [Fact]
    public void Resolve_FullyUnsetStyle_MatchesWhatTextUsedToHardcode()
    {
        var (foreground, background, flags) = Style.Default.Resolve();

        Assert.Equal(Color.Default, foreground);
        Assert.Equal(Color.Default, background);
        Assert.Equal(StyleFlags.None, flags);
    }

    [Fact]
    public void Resolve_SetColors_PassThrough()
    {
        var (foreground, background, _) = new Style(Color.Named(1), Color.Named(2)).Resolve();

        Assert.Equal(Color.Named(1), foreground);
        Assert.Equal(Color.Named(2), background);
    }

    [Fact]
    public void Resolve_TrueFlags_SetTheCorrespondingBits()
    {
        var (_, _, flags) = new Style(Bold: true, Italic: true, Underline: true, Strikethrough: true).Resolve();

        Assert.Equal(StyleFlags.Bold | StyleFlags.Italic | StyleFlags.Underline | StyleFlags.Strikethrough, flags);
    }

    [Fact]
    public void Resolve_FalseAndNullFlags_BothLeaveTheBitUnset()
    {
        var falseFlags = new Style(Bold: false).Resolve().Flags;
        var nullFlags = new Style(Bold: null).Resolve().Flags;

        Assert.Equal(StyleFlags.None, falseFlags);
        Assert.Equal(StyleFlags.None, nullFlags);
    }
}
