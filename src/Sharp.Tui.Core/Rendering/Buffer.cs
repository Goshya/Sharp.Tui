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

    public ref Cell this[int x, int y] => ref _cells[y * Width + x];

    public void Clear() => Array.Fill(_cells, Cell.Blank);
}