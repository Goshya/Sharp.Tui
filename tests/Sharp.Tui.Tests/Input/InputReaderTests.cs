using System.Text;
using System.Threading.Channels;
using Sharp.Tui.Core.Input;

namespace Sharp.Tui.Tests.Input;

public class InputReaderTests
{
    // A stream whose bytes arrive exactly when the test calls Feed — lets tests control
    // whether/when a second chunk shows up relative to InputReader's Esc timeout, which a
    // plain MemoryStream (all bytes available immediately) can't do.
    private sealed class QueueStream : Stream
    {
        private readonly Channel<byte[]> _chunks = Channel.CreateUnbounded<byte[]>();
        private byte[]? _current;
        private int _offset;

        public void Feed(byte[] chunk) => _chunks.Writer.TryWrite(chunk);
        public void Complete() => _chunks.Writer.TryComplete();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            if (_current is null || _offset >= _current.Length)
            {
                if (!await _chunks.Reader.WaitToReadAsync(ct) || !_chunks.Reader.TryRead(out _current))
                    return 0;

                _offset = 0;
            }

            var toCopy = Math.Min(buffer.Length, _current.Length - _offset);
            _current.AsSpan(_offset, toCopy).CopyTo(buffer.Span);
            _offset += toCopy;
            return toCopy;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private static async Task<List<InputEvent>> CollectAsync(InputReader reader, int count, CancellationToken ct = default)
    {
        var events = new List<InputEvent>();
        await foreach (var evt in reader.ReadAsync(ct))
        {
            events.Add(evt);
            if (events.Count >= count)
                break;
        }
        return events;
    }

    [Fact]
    public async Task ReadAsync_SingleCharInOneRead_YieldsOneKeyEvent()
    {
        var stream = new QueueStream();
        stream.Feed("a"u8.ToArray());
        var reader = new InputReader(stream);

        var events = await CollectAsync(reader, 1);

        Assert.Equal(KeyEvent.FromChar(new Rune('a')), events[0].AsKey);
    }

    [Fact]
    public async Task ReadAsync_TwoCharsInOneRead_YieldsBothEventsWithoutAnotherRead()
    {
        var stream = new QueueStream();
        stream.Feed("ab"u8.ToArray());
        var reader = new InputReader(stream);

        var events = await CollectAsync(reader, 2);

        Assert.Equal(KeyEvent.FromChar(new Rune('a')), events[0].AsKey);
        Assert.Equal(KeyEvent.FromChar(new Rune('b')), events[1].AsKey);
    }

    [Fact]
    public async Task ReadAsync_CsiSequenceSplitAcrossTwoReads_YieldsCorrectEvent()
    {
        var stream = new QueueStream();
        stream.Feed([0x1B, (byte)'[']);
        stream.Feed([(byte)'A']);
        var reader = new InputReader(stream, escTimeout: TimeSpan.FromSeconds(5));

        var events = await CollectAsync(reader, 1);

        Assert.Equal(KeyEvent.FromCode(KeyCode.Up), events[0].AsKey);
    }

    [Fact]
    public async Task ReadAsync_LoneEscNeverFollowedUp_TimesOutAsLiteralEscape()
    {
        var stream = new QueueStream();
        stream.Feed([0x1B]);
        var reader = new InputReader(stream, escTimeout: TimeSpan.FromMilliseconds(20));

        var events = await CollectAsync(reader, 1);

        Assert.Equal(KeyEvent.FromCode(KeyCode.Escape), events[0].AsKey);
    }

    // Every clock read jumps forward 6 ms, so with a 10 ms timeout the deadline passes *between*
    // the reader's HasTimedOut check (6 ms elapsed — not yet) and its RemainingTime read
    // (12 ms elapsed — already over). Regression test for a CI hang where that window made the
    // reader await a read that would never complete instead of delivering the Esc.
    private sealed class SteppingTimeProvider : TimeProvider
    {
        private long _now = -6;

        public override long TimestampFrequency => 1000; // 1 tick == 1 ms
        public override long GetTimestamp() => _now += 6;
    }

    [Fact]
    public async Task ReadAsync_EscDeadlinePassesBetweenTimeoutCheckAndRemainingTimeRead_StillYieldsEscape()
    {
        var stream = new QueueStream();
        stream.Feed([0x1B]);
        var reader = new InputReader(stream, TimeSpan.FromMilliseconds(10), new SteppingTimeProvider());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var events = await CollectAsync(reader, 1, cts.Token);

        Assert.Equal(KeyEvent.FromCode(KeyCode.Escape), events[0].AsKey);
    }

    [Fact]
    public async Task ReadAsync_EscFollowedByBracketBeforeTimeout_ParsesAsSequenceNotLiteralEscape()
    {
        var stream = new QueueStream();
        stream.Feed([0x1B]);
        _ = Task.Run(async () =>
        {
            await Task.Delay(5);
            stream.Feed([(byte)'[', (byte)'A']);
        });
        var reader = new InputReader(stream, escTimeout: TimeSpan.FromMilliseconds(200));

        var events = await CollectAsync(reader, 1);

        Assert.Equal(InputEventKind.Key, events[0].Kind);
        Assert.Equal(KeyEvent.FromCode(KeyCode.Up), events[0].AsKey);
    }

    [Fact]
    public async Task ReadAsync_StreamClosedWithNothingBuffered_CompletesEnumerationCleanly()
    {
        var stream = new QueueStream();
        stream.Complete();
        var reader = new InputReader(stream);

        var events = new List<InputEvent>();
        await foreach (var evt in reader.ReadAsync())
            events.Add(evt);

        Assert.Empty(events);
    }

    [Fact]
    public async Task ReadAsync_StreamClosedAfterSomeBytes_YieldsBufferedEventsThenCompletes()
    {
        var stream = new QueueStream();
        stream.Feed("a"u8.ToArray());
        stream.Complete();
        var reader = new InputReader(stream);

        var events = new List<InputEvent>();
        await foreach (var evt in reader.ReadAsync())
            events.Add(evt);

        Assert.Single(events);
        Assert.Equal(KeyEvent.FromChar(new Rune('a')), events[0].AsKey);
    }

    [Fact]
    public async Task ReadAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var stream = new QueueStream();
        var reader = new InputReader(stream);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in reader.ReadAsync(cts.Token))
            {
            }
        });
    }

    [Fact]
    public void Constructor_NullStream_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new InputReader(null!));
    }
}
