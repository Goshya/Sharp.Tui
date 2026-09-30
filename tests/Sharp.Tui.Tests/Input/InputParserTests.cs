using System.Text;
using Sharp.Tui.Core.Input;

namespace Sharp.Tui.Tests.Input;

public class InputParserTests
{
    private static ParseResult Parse(byte[] bytes, out InputEvent evt, out int consumed) =>
        InputParser.TryParse(bytes, out evt, out consumed);

    // --- Plain characters -------------------------------------------------

    [Fact]
    public void PlainAsciiChar_ParsesAsKeyEvent()
    {
        var result = Parse("a"u8.ToArray(), out var evt, out var consumed);

        Assert.Equal(ParseResult.Complete, result);
        Assert.Equal(1, consumed);
        Assert.Equal(InputEventKind.Key, evt.Kind);
        Assert.Equal(KeyEvent.FromChar(new Rune('a')), evt.AsKey);
    }

    [Fact]
    public void MultiByteUtf8Char_Complete_ParsesAsKeyEvent()
    {
        var bytes = Encoding.UTF8.GetBytes("Я");

        var result = Parse(bytes, out var evt, out var consumed);

        Assert.Equal(ParseResult.Complete, result);
        Assert.Equal(bytes.Length, consumed);
        Assert.Equal(KeyEvent.FromChar(new Rune('Я')), evt.AsKey);
    }

    [Fact]
    public void MultiByteUtf8Char_SplitAcrossBuffer_ReturnsNeedMoreBytes()
    {
        var fullBytes = Encoding.UTF8.GetBytes("😀"); // 4-byte UTF-8
        var partial = fullBytes[..2];

        var result = Parse(partial, out _, out var consumed);

        Assert.Equal(ParseResult.NeedMoreBytes, result);
        Assert.Equal(0, consumed);
    }

    [Fact]
    public void InvalidUtf8Byte_Skips1Byte()
    {
        var result = Parse([0xFF], out _, out var consumed);

        Assert.Equal(ParseResult.Skip, result);
        Assert.Equal(1, consumed);
    }

    // --- Control bytes ------------------------------------------------------

    [Theory]
    [InlineData(0x01, 'a')]
    [InlineData(0x03, 'c')]
    [InlineData(0x1A, 'z')]
    public void CtrlLetter_ParsesAsCharWithCtrlModifier(byte b, char expectedChar)
    {
        var result = Parse([b], out var evt, out var consumed);

        Assert.Equal(ParseResult.Complete, result);
        Assert.Equal(1, consumed);
        Assert.Equal(KeyEvent.FromChar(new Rune(expectedChar), KeyModifiers.Ctrl), evt.AsKey);
    }

    [Fact]
    public void CtrlSpace_Nul_ParsesAsSpaceWithCtrlModifier()
    {
        var result = Parse([0x00], out var evt, out _);

        Assert.Equal(ParseResult.Complete, result);
        Assert.Equal(KeyEvent.FromChar(new Rune(' '), KeyModifiers.Ctrl), evt.AsKey);
    }

    [Fact]
    public void Tab_ParsesAsTabKeyCode_NotCtrlI()
    {
        var result = Parse([0x09], out var evt, out _);

        Assert.Equal(ParseResult.Complete, result);
        Assert.Equal(KeyEvent.FromCode(KeyCode.Tab), evt.AsKey);
    }

    [Theory]
    [InlineData(0x0D)] // CR
    [InlineData(0x0A)] // LF — defensive, in case ICRNL translates CR to LF before we see it
    public void CarriageReturnOrLineFeed_ParsesAsEnter(byte b)
    {
        var result = Parse([b], out var evt, out _);

        Assert.Equal(ParseResult.Complete, result);
        Assert.Equal(KeyEvent.FromCode(KeyCode.Enter), evt.AsKey);
    }

    [Theory]
    [InlineData(0x7F)] // DEL — common on Unix
    [InlineData(0x08)] // BS — common on Windows
    public void DelOrBs_ParsesAsBackspace(byte b)
    {
        var result = Parse([b], out var evt, out _);

        Assert.Equal(ParseResult.Complete, result);
        Assert.Equal(KeyEvent.FromCode(KeyCode.Backspace), evt.AsKey);
    }

    // --- Lone Esc / Meta(Alt) encoding --------------------------------------

    [Fact]
    public void LoneEscAtEndOfBuffer_ReturnsNeedMoreBytes()
    {
        var result = Parse([0x1B], out _, out var consumed);

        Assert.Equal(ParseResult.NeedMoreBytes, result);
        Assert.Equal(0, consumed);
    }

    [Fact]
    public void EscFollowedByCtrlLetter_ParsesAsCharWithCtrlAndAltModifiers()
    {
        // ESC 0x01 is how terminals send Alt+Ctrl+A (Ctrl+A alone is already 0x01) — this must
        // route through the same control-byte mapping as a bare Ctrl+A, not decode the raw
        // 0x01 byte as if it were a meaningful standalone character.
        byte[] bytes = [0x1B, 0x01];

        var result = Parse(bytes, out var evt, out var consumed);

        Assert.Equal(ParseResult.Complete, result);
        Assert.Equal(2, consumed);
        Assert.Equal(KeyEvent.FromChar(new Rune('a'), KeyModifiers.Ctrl | KeyModifiers.Alt), evt.AsKey);
    }

    [Fact]
    public void EscFollowedByUnrecognizedControlByte_ParsesAsLiteralEscape_LeavingTheRestForTheNextCall()
    {
        // 0x1C (FS) has no mapping in TryParseControlByte, so this exercises the true
        // "give up, the ESC alone was a literal Escape" fallback.
        byte[] bytes = [0x1B, 0x1C];

        var result = Parse(bytes, out var evt, out var consumed);

        Assert.Equal(ParseResult.Complete, result);
        Assert.Equal(1, consumed);
        Assert.Equal(KeyEvent.FromCode(KeyCode.Escape), evt.AsKey);
    }

    [Fact]
    public void EscFollowedByLetter_ParsesAsAltChar()
    {
        byte[] bytes = [0x1B, (byte)'b'];

        var result = Parse(bytes, out var evt, out var consumed);

        Assert.Equal(ParseResult.Complete, result);
        Assert.Equal(2, consumed);
        Assert.Equal(KeyEvent.FromChar(new Rune('b'), KeyModifiers.Alt), evt.AsKey);
    }

    // --- CSI: arrows and Home/End, unmodified -------------------------------

    [Theory]
    [InlineData('A', KeyCode.Up)]
    [InlineData('B', KeyCode.Down)]
    [InlineData('C', KeyCode.Right)]
    [InlineData('D', KeyCode.Left)]
    [InlineData('H', KeyCode.Home)]
    [InlineData('F', KeyCode.End)]
    public void CsiLetterFinal_Unmodified_ParsesAsExpectedKeyCode(char final, KeyCode expected)
    {
        byte[] bytes = [0x1B, (byte)'[', (byte)final];

        var result = Parse(bytes, out var evt, out var consumed);

        Assert.Equal(ParseResult.Complete, result);
        Assert.Equal(3, consumed);
        Assert.Equal(KeyEvent.FromCode(expected), evt.AsKey);
    }

    [Fact]
    public void CsiModifiedArrow_CtrlRight_MatchesIssueExample()
    {
        // ESC[1;5C — the exact example named in the M1.5 issue text.
        var bytes = Encoding.ASCII.GetBytes("\x1b[1;5C");

        var result = Parse(bytes, out var evt, out var consumed);

        Assert.Equal(ParseResult.Complete, result);
        Assert.Equal(bytes.Length, consumed);
        Assert.Equal(KeyEvent.FromCode(KeyCode.Right, KeyModifiers.Ctrl), evt.AsKey);
    }

    [Theory]
    [InlineData(2, KeyModifiers.Shift)]
    [InlineData(3, KeyModifiers.Alt)]
    [InlineData(4, KeyModifiers.Shift | KeyModifiers.Alt)]
    [InlineData(5, KeyModifiers.Ctrl)]
    [InlineData(6, KeyModifiers.Shift | KeyModifiers.Ctrl)]
    [InlineData(8, KeyModifiers.Shift | KeyModifiers.Alt | KeyModifiers.Ctrl)]
    public void CsiModifierCode_MapsToExpectedModifierBits(int modifierCode, KeyModifiers expected)
    {
        var bytes = Encoding.ASCII.GetBytes($"\x1b[1;{modifierCode}A");

        var result = Parse(bytes, out var evt, out _);

        Assert.Equal(ParseResult.Complete, result);
        Assert.Equal(KeyEvent.FromCode(KeyCode.Up, expected), evt.AsKey);
    }

    [Fact]
    public void CsiIncomplete_MissingFinalByte_ReturnsNeedMoreBytes()
    {
        byte[] bytes = [0x1B, (byte)'[', (byte)'1', (byte)';', (byte)'5'];

        var result = Parse(bytes, out _, out var consumed);

        Assert.Equal(ParseResult.NeedMoreBytes, result);
        Assert.Equal(0, consumed);
    }

    [Fact]
    public void CsiUnknownFinalByte_Skips()
    {
        // 'Z' is a valid CSI terminator byte range-wise but not one we map to anything.
        byte[] bytes = [0x1B, (byte)'[', (byte)'Z'];

        var result = Parse(bytes, out _, out var consumed);

        Assert.Equal(ParseResult.Skip, result);
        Assert.Equal(3, consumed);
    }

    // --- CSI ~-terminated (Delete/Insert/Home/End/PageUp/PageDown/F5-F12) --

    [Theory]
    [InlineData("\x1b[3~", KeyCode.Delete)]
    [InlineData("\x1b[2~", KeyCode.Insert)]
    [InlineData("\x1b[1~", KeyCode.Home)]
    [InlineData("\x1b[7~", KeyCode.Home)]
    [InlineData("\x1b[4~", KeyCode.End)]
    [InlineData("\x1b[8~", KeyCode.End)]
    [InlineData("\x1b[5~", KeyCode.PageUp)]
    [InlineData("\x1b[6~", KeyCode.PageDown)]
    [InlineData("\x1b[15~", KeyCode.F5)]
    [InlineData("\x1b[21~", KeyCode.F10)]
    [InlineData("\x1b[24~", KeyCode.F12)]
    public void CsiTildeTerminated_ParsesAsExpectedKeyCode(string sequence, KeyCode expected)
    {
        var bytes = Encoding.ASCII.GetBytes(sequence);

        var result = Parse(bytes, out var evt, out var consumed);

        Assert.Equal(ParseResult.Complete, result);
        Assert.Equal(bytes.Length, consumed);
        Assert.Equal(KeyEvent.FromCode(expected), evt.AsKey);
    }

    [Fact]
    public void CsiTildeTerminated_WithModifier_CtrlDelete()
    {
        var bytes = Encoding.ASCII.GetBytes("\x1b[3;5~");

        var result = Parse(bytes, out var evt, out _);

        Assert.Equal(ParseResult.Complete, result);
        Assert.Equal(KeyEvent.FromCode(KeyCode.Delete, KeyModifiers.Ctrl), evt.AsKey);
    }

    [Fact]
    public void Csi2Tilde_IsInsert_NotMistakenForPasteStart()
    {
        // Regression guard for the Insert-vs-bracketed-paste ambiguity: ESC[2~ must NOT be
        // treated as a truncated/misdetected paste marker.
        var bytes = Encoding.ASCII.GetBytes("\x1b[2~");

        var result = Parse(bytes, out var evt, out var consumed);

        Assert.Equal(ParseResult.Complete, result);
        Assert.Equal(bytes.Length, consumed);
        Assert.Equal(InputEventKind.Key, evt.Kind);
        Assert.Equal(KeyEvent.FromCode(KeyCode.Insert), evt.AsKey);
    }

    // --- SS3 (F1-F4) ---------------------------------------------------------

    [Theory]
    [InlineData('P', KeyCode.F1)]
    [InlineData('Q', KeyCode.F2)]
    [InlineData('R', KeyCode.F3)]
    [InlineData('S', KeyCode.F4)]
    public void Ss3_ParsesAsExpectedFunctionKey(char final, KeyCode expected)
    {
        byte[] bytes = [0x1B, (byte)'O', (byte)final];

        var result = Parse(bytes, out var evt, out var consumed);

        Assert.Equal(ParseResult.Complete, result);
        Assert.Equal(3, consumed);
        Assert.Equal(KeyEvent.FromCode(expected), evt.AsKey);
    }

    // --- SS3 arrow keys (DECCKM / "application cursor keys" mode) ------------
    // Some shells (bash's own readline) put the terminal into this mode on startup and never
    // turn it back off, so a real interactive session can send these instead of the usual CSI
    // arrow sequences even though we never asked for DECCKM ourselves — both forms must resolve
    // to the same KeyCode.

    [Theory]
    [InlineData('A', KeyCode.Up)]
    [InlineData('B', KeyCode.Down)]
    [InlineData('C', KeyCode.Right)]
    [InlineData('D', KeyCode.Left)]
    public void Ss3_ArrowKeys_ParseTheSameAsCsiArrowKeys(char final, KeyCode expected)
    {
        byte[] bytes = [0x1B, (byte)'O', (byte)final];

        var result = Parse(bytes, out var evt, out var consumed);

        Assert.Equal(ParseResult.Complete, result);
        Assert.Equal(3, consumed);
        Assert.Equal(KeyEvent.FromCode(expected), evt.AsKey);
    }

    // --- SGR mouse -----------------------------------------------------------

    [Fact]
    public void SgrMouse_LeftButtonDown_ParsesCoordinatesAsZeroBased()
    {
        // ESC[<0;10;20M — left button press at column 10, row 20 (1-based on the wire).
        var bytes = Encoding.ASCII.GetBytes("\x1b[<0;10;20M");

        var result = Parse(bytes, out var evt, out var consumed);

        Assert.Equal(ParseResult.Complete, result);
        Assert.Equal(bytes.Length, consumed);
        Assert.Equal(InputEventKind.Mouse, evt.Kind);
        Assert.Equal(new MouseEvent(9, 19, MouseButton.Left, MouseAction.Down), evt.AsMouse);
    }

    [Fact]
    public void SgrMouse_LowercaseM_IsRelease()
    {
        var bytes = Encoding.ASCII.GetBytes("\x1b[<0;1;1m");

        var result = Parse(bytes, out var evt, out _);

        Assert.Equal(ParseResult.Complete, result);
        Assert.Equal(MouseAction.Up, evt.AsMouse.Action);
    }

    [Fact]
    public void SgrMouse_WheelUp_HasNoButtonAndScrollAction()
    {
        // Cb = 0x40 (wheel bit) | 0 (wheel-up) = 64
        var bytes = Encoding.ASCII.GetBytes("\x1b[<64;5;5M");

        var result = Parse(bytes, out var evt, out _);

        Assert.Equal(ParseResult.Complete, result);
        Assert.Equal(MouseButton.None, evt.AsMouse.Button);
        Assert.Equal(MouseAction.ScrollUp, evt.AsMouse.Action);
    }

    [Fact]
    public void SgrMouse_MotionWithButtonHeld_IsMoveAction()
    {
        // Cb = 0x20 (motion bit) | 0 (left button) = 32
        var bytes = Encoding.ASCII.GetBytes("\x1b[<32;5;5M");

        var result = Parse(bytes, out var evt, out _);

        Assert.Equal(ParseResult.Complete, result);
        Assert.Equal(MouseAction.Move, evt.AsMouse.Action);
        Assert.Equal(MouseButton.Left, evt.AsMouse.Button);
    }

    [Fact]
    public void SgrMouse_WithCtrlModifier_IsDecoded()
    {
        // Cb = 0x10 (Ctrl bit) | 0 (left button) = 16
        var bytes = Encoding.ASCII.GetBytes("\x1b[<16;1;1M");

        var result = Parse(bytes, out var evt, out _);

        Assert.Equal(ParseResult.Complete, result);
        Assert.Equal(KeyModifiers.Ctrl, evt.AsMouse.Modifiers);
    }

    [Fact]
    public void SgrMouse_MissingFinalByte_ReturnsNeedMoreBytes()
    {
        var bytes = Encoding.ASCII.GetBytes("\x1b[<0;10;20");

        var result = Parse(bytes, out _, out var consumed);

        Assert.Equal(ParseResult.NeedMoreBytes, result);
        Assert.Equal(0, consumed);
    }

    // --- Bracketed paste -------------------------------------------------------

    [Fact]
    public void BracketedPaste_CompleteInOneBuffer_ParsesWholeTextAsOneEvent()
    {
        var bytes = Encoding.ASCII.GetBytes("\x1b[200~hello world\x1b[201~");

        var result = Parse(bytes, out var evt, out var consumed);

        Assert.Equal(ParseResult.Complete, result);
        Assert.Equal(bytes.Length, consumed);
        Assert.Equal(InputEventKind.Paste, evt.Kind);
        Assert.Equal("hello world", evt.AsPaste);
    }

    [Fact]
    public void BracketedPaste_EndMarkerNotYetArrived_ReturnsNeedMoreBytes()
    {
        var bytes = Encoding.ASCII.GetBytes("\x1b[200~partial content, no end marker yet");

        var result = Parse(bytes, out _, out var consumed);

        Assert.Equal(ParseResult.NeedMoreBytes, result);
        Assert.Equal(0, consumed);
    }

    [Fact]
    public void BracketedPaste_ContainingMultiByteUtf8_DecodesCorrectly()
    {
        var bytes = Encoding.UTF8.GetBytes("\x1b[200~Привет 😀\x1b[201~");

        var result = Parse(bytes, out var evt, out var consumed);

        Assert.Equal(ParseResult.Complete, result);
        Assert.Equal(bytes.Length, consumed);
        Assert.Equal("Привет 😀", evt.AsPaste);
    }

    [Fact]
    public void BracketedPaste_EmptyPaste_ParsesAsEmptyString()
    {
        var bytes = Encoding.ASCII.GetBytes("\x1b[200~\x1b[201~");

        var result = Parse(bytes, out var evt, out var consumed);

        Assert.Equal(ParseResult.Complete, result);
        Assert.Equal("", evt.AsPaste);
    }

    // --- Incremental feeding (simulating the reader loop) -----------------------

    [Fact]
    public void IncrementalFeeding_CsiSequenceArrivingInTwoChunks_EventuallyParses()
    {
        byte[] firstChunk = [0x1B, (byte)'['];
        var firstResult = Parse(firstChunk, out _, out var firstConsumed);
        Assert.Equal(ParseResult.NeedMoreBytes, firstResult);
        Assert.Equal(0, firstConsumed);

        byte[] fullBuffer = [0x1B, (byte)'[', (byte)'A'];
        var secondResult = Parse(fullBuffer, out var evt, out var secondConsumed);

        Assert.Equal(ParseResult.Complete, secondResult);
        Assert.Equal(3, secondConsumed);
        Assert.Equal(KeyEvent.FromCode(KeyCode.Up), evt.AsKey);
    }
}
