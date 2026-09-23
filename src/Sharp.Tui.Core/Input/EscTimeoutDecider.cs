using System;

namespace Sharp.Tui.Core.Input;

// Tracks whether a pending lone ESC byte (InputParser.TryParse returning NeedMoreBytes for a
// 1-byte buffer containing just 0x1B) has been waiting long enough to treat as a literal
// Escape keypress, rather than the start of a sequence that just hasn't finished arriving yet.
// Time is injected via TimeProvider (BCL, no extra dependency) so this is testable without any
// real waiting.
internal sealed class EscTimeoutDecider
{
    private const byte Esc = 0x1B;

    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _timeout;
    private long? _pendingSince;

    public EscTimeoutDecider(TimeProvider timeProvider, TimeSpan timeout)
    {
        _timeProvider = timeProvider;
        _timeout = timeout;
    }

    // Call after every InputParser.TryParse on the pending buffer. Returns true while there's
    // a lone-ESC timeout being tracked (whether or not it has elapsed yet) — false for anything
    // else, including a multi-byte sequence still in progress (there's no useful fallback
    // interpretation for a truncated CSI sequence, so those just keep waiting indefinitely).
    public bool Observe(ReadOnlySpan<byte> buffer, ParseResult result)
    {
        if (result == ParseResult.NeedMoreBytes && buffer.Length == 1 && buffer[0] == Esc)
        {
            // First-seen timestamp only: if the caller re-observes the same still-pending byte
            // on every loop iteration (no new bytes having arrived), this must NOT keep pushing
            // the clock forward, or the timeout would never fire.
            _pendingSince ??= _timeProvider.GetTimestamp();
            return true;
        }

        _pendingSince = null;
        return false;
    }

    public bool HasTimedOut =>
        _pendingSince is { } since && _timeProvider.GetElapsedTime(since) >= _timeout;

    // How much longer the caller can afford to wait for more bytes before the pending Esc
    // times out — null when nothing is pending. Can come back <= TimeSpan.Zero if HasTimedOut
    // is already true; callers should check HasTimedOut first.
    public TimeSpan? RemainingTime =>
        _pendingSince is { } since ? _timeout - _timeProvider.GetElapsedTime(since) : null;
}
