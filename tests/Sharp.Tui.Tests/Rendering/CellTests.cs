using System.Text;
using Sharp.Tui.Core.Rendering;

namespace Sharp.Tui.Tests.Rendering;

public class CellTests
{
    private static Cell Sample() =>
        new(new Rune('A'), Color.Rgb(1, 2, 3), Color.Named(4), StyleFlags.Bold);

    [Fact]
    public void Blank_IsASpaceWithDefaultColorsAndNoStyle()
    {
        Assert.Equal(new Rune(' '), Cell.Blank.Char);
        Assert.Equal(Color.Default, Cell.Blank.Foreground);
        Assert.Equal(Color.Default, Cell.Blank.Background);
        Assert.Equal(StyleFlags.None, Cell.Blank.Style);
    }

    [Fact]
    public void Constructor_StoresAllFields()
    {
        var cell = Sample();

        Assert.Equal(new Rune('A'), cell.Char);
        Assert.Equal(Color.Rgb(1, 2, 3), cell.Foreground);
        Assert.Equal(Color.Named(4), cell.Background);
        Assert.Equal(StyleFlags.Bold, cell.Style);
    }

    [Fact]
    public void Equals_IdenticalFields_IsTrue()
    {
        Assert.Equal(Sample(), Sample());
    }

    [Fact]
    public void Equals_DifferingChar_IsFalse()
    {
        var other = new Cell(new Rune('B'), Color.Rgb(1, 2, 3), Color.Named(4), StyleFlags.Bold);

        Assert.NotEqual(Sample(), other);
    }

    [Fact]
    public void Equals_DifferingForeground_IsFalse()
    {
        var other = new Cell(new Rune('A'), Color.Rgb(9, 2, 3), Color.Named(4), StyleFlags.Bold);

        Assert.NotEqual(Sample(), other);
    }

    [Fact]
    public void Equals_DifferingBackground_IsFalse()
    {
        var other = new Cell(new Rune('A'), Color.Rgb(1, 2, 3), Color.Named(5), StyleFlags.Bold);

        Assert.NotEqual(Sample(), other);
    }

    [Fact]
    public void Equals_DifferingStyle_IsFalse()
    {
        var other = new Cell(new Rune('A'), Color.Rgb(1, 2, 3), Color.Named(4), StyleFlags.Bold | StyleFlags.Underline);

        Assert.NotEqual(Sample(), other);
    }

    [Fact]
    public void Equals_Object_HandlesBoxedCellsAndForeignTypes()
    {
        object boxed = Sample();

        Assert.True(Sample().Equals(boxed));
        Assert.False(Sample().Equals(Cell.Blank));
        Assert.False(Sample().Equals("not a cell"));
        Assert.False(Sample().Equals(null));
    }

    [Fact]
    public void GetHashCode_EqualCells_ProduceEqualHashCodes()
    {
        Assert.Equal(Sample().GetHashCode(), Sample().GetHashCode());
        Assert.Equal(Cell.Blank.GetHashCode(), Cell.Blank.GetHashCode());
    }
}
