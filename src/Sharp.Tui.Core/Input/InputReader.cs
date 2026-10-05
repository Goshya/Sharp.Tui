using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Sharp.Tui.Core.Input;

// Turns a raw byte stream (stdin in production) into a stream of InputEvents: reads bytes,
// runs them through InputParser, and uses EscTimeoutDecider to resolve a lone Esc keypress
// that never turns into a sequence. Resize detection is not this class's job — that comes from
// a signal/polling, not from the stdin byte stream itself.
public sealed class InputReader
{
    private static readonly TimeSpan DefaultEscTimeout = TimeSpan.FromMilliseconds(50);

    private readonly Stream _input;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _escTimeout;

    public InputReader(Stream input, TimeSpan? escTimeout = null, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(input);

        _input = input;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _escTimeout = escTimeout ?? DefaultEscTimeout;
    }

    // The stream to hand InputReader in production — not just Console.OpenStandardInput(): on Unix,
    // when stdin is a terminal, .NET returns a line-oriented reader that echoes what it reads and
    // hands data over only after Enter, which is fatal for a key-by-key TUI. Linux gets a
    // poll()-based stream that also honours cancellation (see UnixStdinStream). Windows is fine
    // with the Console stream: raw mode (IRawModeScope) already makes it deliver keys as typed.
    public static Stream OpenStandardInput() =>
        OperatingSystem.IsLinux()
            ? new UnixStdinStream()
            : Console.OpenStandardInput();

    public async IAsyncEnumerable<InputEvent> ReadAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        var buffer = new byte[256];
        var length = 0;
        var readChunk = new byte[1024];
        var decider = new EscTimeoutDecider(_timeProvider, _escTimeout);
        Task<int>? pendingRead = null;

        while (true)
        {
            // Decode everything already buffered before touching the stream again — a single
            // read can easily contain more than one event (fast typing, a paste, etc.).
            while (length > 0)
            {
                var result = InputParser.TryParse(buffer.AsSpan(0, length), out var evt, out var consumed);
                if (result == ParseResult.NeedMoreBytes)
                    break;

                if (result == ParseResult.Complete)
                    yield return evt;

                ShiftLeft(buffer, ref length, consumed);
            }

            var isPendingEsc = decider.Observe(buffer.AsSpan(0, length), ParseResult.NeedMoreBytes);
            if (isPendingEsc && decider.HasTimedOut)
            {
                yield return InputEvent.Key(KeyEvent.FromCode(KeyCode.Escape));
                ShiftLeft(buffer, ref length, 1);
                continue;
            }

            ct.ThrowIfCancellationRequested();

            // Exactly one read stays in flight across iterations — most streams (stdin
            // included) don't support overlapping ReadAsync calls, so a timed-out wait below
            // must not abandon this task, only stop waiting on it for now.
            pendingRead ??= _input.ReadAsync(readChunk, ct).AsTask();

            if (isPendingEsc)
            {
                var remaining = decider.RemainingTime!.Value;

                // HasTimedOut above and RemainingTime here read the clock at two different
                // instants, so the deadline can pass in between: <= Zero means "just timed out",
                // not "nothing to wait for". Falling through to await the read would block until
                // the next keypress and the lone Esc would never be delivered.
                if (remaining <= TimeSpan.Zero)
                    continue;

                var delayTask = Task.Delay(remaining, _timeProvider, ct);
                var winner = await Task.WhenAny(pendingRead, delayTask).ConfigureAwait(false);
                if (winner == delayTask)
                    continue; // no bytes arrived in time — loop back and let HasTimedOut fire
            }

            var bytesRead = await pendingRead.ConfigureAwait(false);
            pendingRead = null;

            if (bytesRead == 0)
                yield break; // stream closed

            Append(ref buffer, ref length, readChunk.AsSpan(0, bytesRead));
        }
    }

    private static void ShiftLeft(byte[] buffer, ref int length, int amount)
    {
        if (amount == 0)
            return;

        var remaining = length - amount;
        if (remaining > 0)
            Array.Copy(buffer, amount, buffer, 0, remaining);

        length = remaining;
    }

    private static void Append(ref byte[] buffer, ref int length, ReadOnlySpan<byte> data)
    {
        if (buffer.Length - length < data.Length)
            Array.Resize(ref buffer, Math.Max(buffer.Length * 2, length + data.Length));

        data.CopyTo(buffer.AsSpan(length));
        length += data.Length;
    }
}
