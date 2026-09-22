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
    private const int MaxUtf8BytesPerRune = 4;
    private const byte Esc = 0x1B;

    private const int CursorVisibilityMode = 25;
    private const int AlternateScreenMode = 1049;

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

        WriteCsi();
        WriteInt(y + 1);
        WriteByte((byte)';');
        WriteInt(x + 1);
        WriteByte((byte)'H');
    }

    public void SetStyle(Color foreground, Color background, StyleFlags style)
    {
        WriteCsi();
        WriteInt(0);

        if ((style & StyleFlags.Bold) != 0) WriteSgrParam(1);
        if ((style & StyleFlags.Italic) != 0) WriteSgrParam(3);
        if ((style & StyleFlags.Underline) != 0) WriteSgrParam(4);
        if ((style & StyleFlags.Strikethrough) != 0) WriteSgrParam(9);

        WriteColor(foreground.Downgrade(_colorSupport), isForeground: true);
        WriteColor(background.Downgrade(_colorSupport), isForeground: false);

        WriteByte((byte)'m');
    }

    // Every call starts from SGR reset (0): callers pass the complete resulting style, not a delta,
    // so resetting first keeps SetStyle idempotent regardless of what was written before it.
    private void WriteColor(Color color, bool isForeground)
    {
        switch (color.Kind)
        {
            case ColorKind.Default:
                return;

            case ColorKind.Named16:
                var baseCode = color.Index < 8
                    ? (isForeground ? 30 : 40)
                    : (isForeground ? 90 : 100) - 8;
                WriteSgrParam(baseCode + color.Index);
                return;

            case ColorKind.Indexed256:
                WriteSgrParam(isForeground ? 38 : 48);
                WriteSgrParam(5);
                WriteSgrParam(color.Index);
                return;

            case ColorKind.Rgb:
                WriteSgrParam(isForeground ? 38 : 48);
                WriteSgrParam(2);
                WriteSgrParam(color.R);
                WriteSgrParam(color.G);
                WriteSgrParam(color.B);
                return;
        }
    }

    private void WriteSgrParam(int value)
    {
        WriteByte((byte)';');
        WriteInt(value);
    }

    public void Write(Rune rune)
    {
        rune.TryEncodeToUtf8(GetSpan(MaxUtf8BytesPerRune), out var written);
        _length += written;
    }

    public void Clear()
    {
        WriteCsi();
        WriteInt(2);
        WriteByte((byte)'J');
    }

    private void WriteDecPrivateMode(int mode, bool enabled)
    {
        WriteCsi();
        WriteByte((byte)'?');
        WriteInt(mode);
        WriteByte((byte)(enabled ? 'h' : 'l'));
    }

    public void ShowCursor() => WriteDecPrivateMode(CursorVisibilityMode, enabled: true);
    public void HideCursor() => WriteDecPrivateMode(CursorVisibilityMode, enabled: false);

    public void EnterAlternateScreen() => WriteDecPrivateMode(AlternateScreenMode, enabled: true);
    public void ExitAlternateScreen() => WriteDecPrivateMode(AlternateScreenMode, enabled: false);

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

    // ESC + '[' — prefix shared by every CSI sequence (cursor move, style, show/hide cursor, ...).
    private void WriteCsi()
    {
        WriteByte(Esc);
        WriteByte((byte)'[');
    }
}
