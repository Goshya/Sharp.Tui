using System.Text;
using Sharp.Tui.Core.Rendering;
using Buffer = Sharp.Tui.Core.Rendering.Buffer;

namespace Sharp.Tui.Tests.Rendering;

public class BufferTests
{
    [Fact]
    public void Constructor_SetsWidthAndHeight()
    {
        var buffer = new Buffer(width: 10, height: 4);

        Assert.Equal(10, buffer.Width);
        Assert.Equal(4, buffer.Height);
    }

    [Fact]
    public void Constructor_FillsEveryCellWithBlank()
    {
        var buffer = new Buffer(width: 3, height: 3);

        for (var y = 0; y < buffer.Height; y++)
        for (var x = 0; x < buffer.Width; x++)
            Assert.Equal(Cell.Blank, buffer[x, y]);
    }

    [Fact]
    public void Indexer_RoundTripsAWrittenCell()
    {
        var buffer = new Buffer(width: 5, height: 5);
        var cell = new Cell(new Rune('A'), Color.Rgb(255, 0, 0), Color.Default, StyleFlags.Bold);

        buffer[2, 3] = cell;

        Assert.Equal(cell, buffer[2, 3]);
    }

    [Fact]
    public void Indexer_WritingOneCell_DoesNotAffectNeighbors()
    {
        // Deliberately non-square, so an x/y-swap bug in the row-major index math would surface.
        var buffer = new Buffer(width: 7, height: 3);
        var cell = new Cell(new Rune('X'), Color.Named(1), Color.Default, StyleFlags.None);

        buffer[4, 1] = cell;

        for (var y = 0; y < buffer.Height; y++)
        for (var x = 0; x < buffer.Width; x++)
        {
            if (x == 4 && y == 1)
                Assert.Equal(cell, buffer[x, y]);
            else
                Assert.Equal(Cell.Blank, buffer[x, y]);
        }
    }

    [Fact]
    public void Clear_ResetsAllPreviouslyWrittenCellsToBlank()
    {
        var buffer = new Buffer(width: 4, height: 4);
        buffer[0, 0] = new Cell(new Rune('A'), Color.Default, Color.Default, StyleFlags.None);
        buffer[3, 3] = new Cell(new Rune('B'), Color.Default, Color.Default, StyleFlags.None);

        buffer.Clear();

        for (var y = 0; y < buffer.Height; y++)
        for (var x = 0; x < buffer.Width; x++)
            Assert.Equal(Cell.Blank, buffer[x, y]);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 100)]
    [InlineData(5, 0)]   // one past the last valid column (width = 5, valid x is 0..4)
    [InlineData(0, 5)]   // one past the last valid row (height = 5, valid y is 0..4)
    public void Indexer_WithIndexOutOfRange_Throws(int x, int y)
    {
        var buffer = new Buffer(width: 5, height: 5);

        Assert.Throws<ArgumentOutOfRangeException>(() => buffer[x, y]);
    }

    [Fact]
    public void Indexer_WithXPastRowWidth_ThrowsInsteadOfWrappingIntoTheNextRow()
    {
        // Regression test: x=6 on a width-5 buffer used to compute a valid flat index
        // (1*5 + 1 = 6) and silently return/overwrite the cell at [1, 1] instead of
        // signaling an error. The indexer must reject x/y independently of the
        // underlying flat array bounds.
        var buffer = new Buffer(width: 5, height: 5);
        var sentinel = new Cell(new Rune('Z'), Color.Default, Color.Default, StyleFlags.None);
        buffer[1, 1] = sentinel;

        Assert.Throws<ArgumentOutOfRangeException>(() => buffer[6, 0]);
        Assert.Equal(sentinel, buffer[1, 1]);
    }

    [Fact]
    public void Indexer_AtLastValidCoordinate_DoesNotThrow()
    {
        var buffer = new Buffer(width: 5, height: 5);

        var record = Record.Exception(() => buffer[4, 4]);

        Assert.Null(record);
    }
}
