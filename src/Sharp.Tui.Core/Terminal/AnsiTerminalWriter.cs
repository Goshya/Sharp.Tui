using System;
using System.Collections.Generic;
using System.Text;
using Sharp.Tui.Core.Rendering;

namespace Sharp.Tui.Core.Terminal;

public class AnsiTerminalWriter : ITerminalWriter
{
    private readonly Stream _output;
    private readonly ColorSupport _colorSupport;
    private byte[] _buffer = new byte[4096];
    private int _length;
    private const int MaxInt32Chars = 11;

    public AnsiTerminalWriter(Stream output, ColorSupport colorSupport)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (!output.CanWrite)
            throw new ArgumentException("The output stream must be writable.", nameof(output));

        _output = output;
        _colorSupport = colorSupport;
    }

    public void MoveCursor(int x, int y)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfNegative(y);

        WriteBytes("["u8);
        WriteInt(y + 1);
        WriteByte((byte)';');
        WriteInt(x + 1);
        WriteByte((byte)'H');
    }

    public void SetStyle(Color foreground, Color background, StyleFlags style)
    {
        throw new NotImplementedException();
    }

    public void Write(Rune rune)
    {
        throw new NotImplementedException();
    }

    public void Clear()
    {
        throw new NotImplementedException();
    }

    public void ShowCursor()
    {
        throw new NotImplementedException();
    }

    public void HideCursor()
    {
        throw new NotImplementedException();
    }

    public void EnterAlternateScreen()
    {
        throw new NotImplementedException();
    }

    public void ExitAlternateScreen()
    {
        throw new NotImplementedException();
    }

    public void Flush()
    {
        if (_length == 0)
            return;

        _output.Write(_buffer.AsSpan(0, _length));
        _output.Flush();
        _length = 0;
    }

    private Span<byte> GetSpan(int count)
    {
        if (_buffer.Length - _length < count)
            Array.Resize(ref _buffer, Math.Max(_buffer.Length * 2, _length + count));

        return _buffer.AsSpan(_length);
    }

    private void WriteBytes(ReadOnlySpan<byte> bytes)
    {
        bytes.CopyTo(GetSpan(bytes.Length));
        _length += bytes.Length;
    }

    private void WriteByte(byte value)
    {
        GetSpan(1)[0] = value;
        _length++;
    }

    private void WriteInt(int value)
    {
        value.TryFormat(GetSpan(MaxInt32Chars), out var written);
        _length += written;
    }
}
