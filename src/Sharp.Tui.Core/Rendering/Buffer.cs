using System;
using System.Collections.Generic;
using System.Text;

namespace Sharp.Tui.Core.Rendering;

public sealed class Buffer
{
    private readonly Cell[] _cells;
    public int Width { get; }
    public int Height { get; }

    public Buffer(int width, int height)
    {
        Width = width; Height = height;
        _cells = new Cell[width * height];
        Array.Fill(_cells, Cell.Blank);
    }

    public ref Cell this[int x, int y]
    {
        get
        {
            if ((uint)x >= (uint)Width)
                throw new ArgumentOutOfRangeException(nameof(x), x, $"x must be in [0, {Width}).");
            if ((uint)y >= (uint)Height)
                throw new ArgumentOutOfRangeException(nameof(y), y, $"y must be in [0, {Height}).");

            return ref _cells[y * Width + x];
        }
    }

    public void Clear() => Array.Fill(_cells, Cell.Blank);
}