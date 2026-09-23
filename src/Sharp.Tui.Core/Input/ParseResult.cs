namespace Sharp.Tui.Core.Input;

internal enum ParseResult
{
    // One event decoded — see the out InputEvent and how many bytes it took.
    Complete,

    // The buffer is a valid-so-far prefix of something (e.g. a lone ESC, or a CSI sequence
    // whose final byte hasn't arrived yet) — wait for more bytes and call TryParse again on
    // the same (now longer) buffer. This is also how a lone Esc key gets disambiguated from
    // the start of an escape sequence: the reader loop decides how long to wait before giving
    // up and treating it as a literal Escape keypress.
    NeedMoreBytes,

    // The leading byte(s) don't form anything recognized — skip `bytesConsumed` bytes and
    // retry from there.
    Skip,
}
