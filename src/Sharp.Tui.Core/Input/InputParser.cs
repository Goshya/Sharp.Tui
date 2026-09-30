using System;
using System.Buffers;
using System.Text;

namespace Sharp.Tui.Core.Input;

// Pure byte-sequence -> InputEvent parser: no stdin, no timers, no async reader loop. The
// reader loop (a later step) owns actual I/O and the lone-Esc-vs-sequence timeout; this only
// answers "given the bytes buffered so far, is there a complete event, do we need more bytes,
// or is the front of the buffer junk to skip". Same shape as FrameDiff/WindowsConsoleModeFlags:
// pure computation, fully testable without any real terminal attached.
internal static class InputParser
{
    private const byte Esc = 0x1B;

    private static ReadOnlySpan<byte> PasteEndMarker => "\x1b[201~"u8;

    public static ParseResult TryParse(ReadOnlySpan<byte> buffer, out InputEvent inputEvent, out int bytesConsumed)
    {
        inputEvent = default;
        bytesConsumed = 0;

        if (buffer.IsEmpty)
            return ParseResult.NeedMoreBytes;

        return buffer[0] switch
        {
            Esc => TryParseEscapeSequence(buffer, out inputEvent, out bytesConsumed),
            < 0x20 or 0x7F => TryParseControlByte(buffer[0], out inputEvent, out bytesConsumed),
            _ => TryParsePlainChar(buffer, out inputEvent, out bytesConsumed),
        };
    }

    private static ParseResult TryParsePlainChar(ReadOnlySpan<byte> buffer, out InputEvent inputEvent, out int bytesConsumed)
    {
        var status = Rune.DecodeFromUtf8(buffer, out var rune, out var consumed);

        switch (status)
        {
            case OperationStatus.Done:
                inputEvent = InputEvent.Key(KeyEvent.FromChar(rune));
                bytesConsumed = consumed;
                return ParseResult.Complete;

            case OperationStatus.NeedMoreData:
                inputEvent = default;
                bytesConsumed = 0;
                return ParseResult.NeedMoreBytes;

            default: // InvalidData
                inputEvent = default;
                bytesConsumed = 1;
                return ParseResult.Skip;
        }
    }

    private static ParseResult TryParseControlByte(byte b, out InputEvent inputEvent, out int bytesConsumed)
    {
        bytesConsumed = 1;

        // Backspace: real hardware/terminals send either DEL (0x7F, common on Unix) or BS
        // (0x08, common on Windows/some emulators) for the same physical key — both map here
        // rather than letting 0x08 fall through to "Ctrl+H", which is indistinguishable from
        // Backspace on the wire and essentially never bound to anything different in practice.
        if (b is 0x7F or 0x08)
        {
            inputEvent = InputEvent.Key(KeyEvent.FromCode(KeyCode.Backspace));
            return ParseResult.Complete;
        }

        if (b == 0x09)
        {
            inputEvent = InputEvent.Key(KeyEvent.FromCode(KeyCode.Tab));
            return ParseResult.Complete;
        }

        // Enter: terminals send CR (0x0D). LF (0x0A) is included defensively too, since Unix's
        // ICRNL input flag can translate a received CR into LF before it ever reaches us, and
        // our raw-mode toggle (M1.4) only clears ICANON/ECHO, not ICRNL.
        if (b is 0x0D or 0x0A)
        {
            inputEvent = InputEvent.Key(KeyEvent.FromCode(KeyCode.Enter));
            return ParseResult.Complete;
        }

        if (b == 0x00)
        {
            inputEvent = InputEvent.Key(KeyEvent.FromChar(new Rune(' '), KeyModifiers.Ctrl));
            return ParseResult.Complete;
        }

        if (b is >= 0x01 and <= 0x1A)
        {
            inputEvent = InputEvent.Key(KeyEvent.FromChar(new Rune('a' + b - 1), KeyModifiers.Ctrl));
            return ParseResult.Complete;
        }

        inputEvent = default;
        return ParseResult.Skip;
    }

    private static ParseResult TryParseEscapeSequence(ReadOnlySpan<byte> buffer, out InputEvent inputEvent, out int bytesConsumed)
    {
        inputEvent = default;
        bytesConsumed = 0;

        // A lone ESC so far is a valid prefix of *something* — could be a real Escape keypress
        // with nothing following, or the start of a multi-byte sequence. The reader loop's
        // timeout decides which; this layer just says "not enough information yet".
        if (buffer.Length < 2)
            return ParseResult.NeedMoreBytes;

        if (buffer[1] == (byte)'[')
            return TryParseCsi(buffer, out inputEvent, out bytesConsumed);

        if (buffer[1] == (byte)'O')
            return TryParseSs3(buffer, out inputEvent, out bytesConsumed);

        // ESC followed by another control byte is Alt+<that control combo> (e.g. Alt+Ctrl+A
        // sends ESC 0x01, since Ctrl+A alone is 0x01) — route it through the same control-byte
        // mapping and add Alt, rather than falling into the plain-UTF-8 branch below, where a
        // control byte like 0x01 would otherwise decode "successfully" as a bare, meaningless
        // U+0001 character (every byte < 0x80 is trivially valid single-byte UTF-8).
        if (buffer[1] < 0x20 || buffer[1] == 0x7F)
        {
            var controlResult = TryParseControlByte(buffer[1], out inputEvent, out var controlConsumed);
            if (controlResult == ParseResult.Complete)
            {
                var key = inputEvent.AsKey;
                inputEvent = InputEvent.Key(key with { Modifiers = key.Modifiers | KeyModifiers.Alt });
                bytesConsumed = 1 + controlConsumed;
                return ParseResult.Complete;
            }

            // Unrecognized control byte — the ESC alone is a literal Escape keypress; leave
            // the rest of the buffer for the next call.
            inputEvent = InputEvent.Key(KeyEvent.FromCode(KeyCode.Escape));
            bytesConsumed = 1;
            return ParseResult.Complete;
        }

        // ESC directly followed by a printable character is the classic "Meta"/Alt-key
        // encoding many terminals use (e.g. Alt+B sends ESC 'b') — that character with Alt set.
        var status = Rune.DecodeFromUtf8(buffer[1..], out var rune, out var consumed);
        if (status == OperationStatus.Done)
        {
            inputEvent = InputEvent.Key(KeyEvent.FromChar(rune, KeyModifiers.Alt));
            bytesConsumed = 1 + consumed;
            return ParseResult.Complete;
        }

        if (status == OperationStatus.NeedMoreData)
            return ParseResult.NeedMoreBytes;

        // Second byte isn't a recognized sequence starter or valid UTF-8 — the ESC on its own
        // is a literal Escape keypress; leave the rest of the buffer for the next call.
        inputEvent = InputEvent.Key(KeyEvent.FromCode(KeyCode.Escape));
        bytesConsumed = 1;
        return ParseResult.Complete;
    }

    private static ParseResult TryParseSs3(ReadOnlySpan<byte> buffer, out InputEvent inputEvent, out int bytesConsumed)
    {
        inputEvent = default;
        bytesConsumed = 0;

        if (buffer.Length < 3)
            return ParseResult.NeedMoreBytes;

        KeyCode? code = buffer[2] switch
        {
            (byte)'P' => KeyCode.F1,
            (byte)'Q' => KeyCode.F2,
            (byte)'R' => KeyCode.F3,
            (byte)'S' => KeyCode.F4,
            (byte)'A' => KeyCode.Up,
            (byte)'B' => KeyCode.Down,
            (byte)'C' => KeyCode.Right,
            (byte)'D' => KeyCode.Left,
            _ => null,
        };

        bytesConsumed = 3;
        if (code is null)
        {
            inputEvent = default;
            return ParseResult.Skip;
        }

        inputEvent = InputEvent.Key(KeyEvent.FromCode(code.Value));
        return ParseResult.Complete;
    }

    private static ParseResult TryParseCsi(ReadOnlySpan<byte> buffer, out InputEvent inputEvent, out int bytesConsumed)
    {
        inputEvent = default;
        bytesConsumed = 0;

        if (buffer.Length < 3)
            return ParseResult.NeedMoreBytes;

        // SGR mouse reports: ESC[<... — the '<' is never a valid CSI parameter byte, so this
        // is unambiguous without needing to look further ahead first.
        if (buffer[2] == (byte)'<')
            return TryParseSgrMouse(buffer, out inputEvent, out bytesConsumed);

        var i = 2;
        while (i < buffer.Length && (buffer[i] is (byte)';' or (>= (byte)'0' and <= (byte)'9')))
            i++;

        if (i >= buffer.Length)
            return ParseResult.NeedMoreBytes;

        var final = buffer[i];
        var paramsSpan = buffer[2..i];

        // Bracketed paste start (ESC[200~) looks like an ordinary CSI sequence right up until
        // here — only once the full "200" parameter and '~' terminator are in hand do we know
        // to switch into scanning for the end marker instead of mapping it as a normal key, so
        // this can't be special-cased any earlier without misreading e.g. ESC[2~ (Insert).
        if (final == (byte)'~' && paramsSpan.SequenceEqual("200"u8))
            return TryParseBracketedPasteContent(buffer, headerLength: i + 1, out inputEvent, out bytesConsumed);

        bytesConsumed = i + 1;

        if (final is < (byte)'\x40' or > (byte)'\x7E')
        {
            inputEvent = default;
            return ParseResult.Skip;
        }

        return TryMapCsiFinalByte(paramsSpan, final, out inputEvent) ? ParseResult.Complete : ParseResult.Skip;
    }

    private static bool TryMapCsiFinalByte(ReadOnlySpan<byte> paramsSpan, byte final, out InputEvent inputEvent)
    {
        var (p1, p2) = ParseParams(paramsSpan);

        KeyCode? code = final switch
        {
            (byte)'A' => KeyCode.Up,
            (byte)'B' => KeyCode.Down,
            (byte)'C' => KeyCode.Right,
            (byte)'D' => KeyCode.Left,
            (byte)'H' => KeyCode.Home,
            (byte)'F' => KeyCode.End,
            (byte)'~' => p1 switch
            {
                1 or 7 => KeyCode.Home,
                2 => KeyCode.Insert,
                3 => KeyCode.Delete,
                4 or 8 => KeyCode.End,
                5 => KeyCode.PageUp,
                6 => KeyCode.PageDown,
                11 => KeyCode.F1,
                12 => KeyCode.F2,
                13 => KeyCode.F3,
                14 => KeyCode.F4,
                15 => KeyCode.F5,
                17 => KeyCode.F6,
                18 => KeyCode.F7,
                19 => KeyCode.F8,
                20 => KeyCode.F9,
                21 => KeyCode.F10,
                23 => KeyCode.F11,
                24 => KeyCode.F12,
                _ => null,
            },
            _ => null,
        };

        if (code is null)
        {
            inputEvent = default;
            return false;
        }

        // The modifier is always the second parameter (ESC[1;5A, ESC[3;5~) — the first is
        // either absent or a fixed "1"/the ~-code, never the modifier itself.
        var modifiers = ModifiersFromCsiCode(p2 ?? 1);
        inputEvent = InputEvent.Key(KeyEvent.FromCode(code.Value, modifiers));
        return true;
    }

    private static ParseResult TryParseSgrMouse(ReadOnlySpan<byte> buffer, out InputEvent inputEvent, out int bytesConsumed)
    {
        inputEvent = default;
        bytesConsumed = 0;

        var i = 3; // past ESC [ <
        while (i < buffer.Length && buffer[i] != (byte)'M' && buffer[i] != (byte)'m')
            i++;

        if (i >= buffer.Length)
            return ParseResult.NeedMoreBytes;

        var isRelease = buffer[i] == (byte)'m';
        var paramsSpan = buffer[3..i];
        bytesConsumed = i + 1;

        var firstSemi = paramsSpan.IndexOf((byte)';');
        if (firstSemi < 0)
            return ParseResult.Skip;

        var rest = paramsSpan[(firstSemi + 1)..];
        var secondSemi = rest.IndexOf((byte)';');
        if (secondSemi < 0)
            return ParseResult.Skip;

        var cb = ParseInt(paramsSpan[..firstSemi]);
        var cx = ParseInt(rest[..secondSemi]);
        var cy = ParseInt(rest[(secondSemi + 1)..]);
        if (cb is null || cx is null || cy is null)
            return ParseResult.Skip;

        var cbValue = cb.Value;
        var isWheel = (cbValue & 0x40) != 0;
        var isMotion = (cbValue & 0x20) != 0;
        var buttonBits = cbValue & 0x3;

        var modifiers = KeyModifiers.None;
        if ((cbValue & 0x04) != 0) modifiers |= KeyModifiers.Shift;
        if ((cbValue & 0x08) != 0) modifiers |= KeyModifiers.Alt;
        if ((cbValue & 0x10) != 0) modifiers |= KeyModifiers.Ctrl;

        MouseButton button;
        MouseAction action;

        if (isWheel)
        {
            button = MouseButton.None;
            action = buttonBits == 0 ? MouseAction.ScrollUp : MouseAction.ScrollDown;
        }
        else
        {
            button = buttonBits switch
            {
                0 => MouseButton.Left,
                1 => MouseButton.Middle,
                2 => MouseButton.Right,
                _ => MouseButton.None,
            };
            action = isMotion ? MouseAction.Move : isRelease ? MouseAction.Up : MouseAction.Down;
        }

        // SGR coordinates are 1-based; MouseEvent uses the same 0-based convention as Buffer's
        // indexer and ITerminalWriter.MoveCursor.
        inputEvent = InputEvent.Mouse(new MouseEvent(cx.Value - 1, cy.Value - 1, button, action, modifiers));
        return ParseResult.Complete;
    }

    private static ParseResult TryParseBracketedPasteContent(ReadOnlySpan<byte> buffer, int headerLength, out InputEvent inputEvent, out int bytesConsumed)
    {
        inputEvent = default;
        bytesConsumed = 0;

        var content = buffer[headerLength..];
        var endIndex = content.IndexOf(PasteEndMarker);
        if (endIndex < 0)
            return ParseResult.NeedMoreBytes;

        bytesConsumed = headerLength + endIndex + PasteEndMarker.Length;
        inputEvent = InputEvent.Paste(Encoding.UTF8.GetString(content[..endIndex]));
        return ParseResult.Complete;
    }

    private static KeyModifiers ModifiersFromCsiCode(int code)
    {
        if (code <= 1)
            return KeyModifiers.None;

        var bits = code - 1;
        var modifiers = KeyModifiers.None;
        if ((bits & 1) != 0) modifiers |= KeyModifiers.Shift;
        if ((bits & 2) != 0) modifiers |= KeyModifiers.Alt;
        if ((bits & 4) != 0) modifiers |= KeyModifiers.Ctrl;
        return modifiers;
    }

    private static (int? P1, int? P2) ParseParams(ReadOnlySpan<byte> paramsSpan)
    {
        if (paramsSpan.IsEmpty)
            return (null, null);

        var semicolon = paramsSpan.IndexOf((byte)';');
        return semicolon < 0
            ? (ParseInt(paramsSpan), null)
            : (ParseInt(paramsSpan[..semicolon]), ParseInt(paramsSpan[(semicolon + 1)..]));
    }

    private static int? ParseInt(ReadOnlySpan<byte> span)
    {
        if (span.IsEmpty)
            return null;

        var value = 0;
        foreach (var b in span)
        {
            if (b is < (byte)'0' or > (byte)'9')
                return null;

            value = value * 10 + (b - (byte)'0');
        }

        return value;
    }
}
