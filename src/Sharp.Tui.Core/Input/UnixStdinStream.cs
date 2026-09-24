using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace Sharp.Tui.Core.Input;

// Raw, key-by-key stdin for Linux. Two things Console.OpenStandardInput() can't give us:
//  * on a terminal it returns a line-oriented reader (echoes, delivers only after Enter);
//  * a plain blocking read() can't be cancelled, so quitting the app would hang until the next
//    keypress — and that abandoned read would then swallow the first key typed at the shell.
// So waiting for data is done with poll() in short slices, checking the CancellationToken between
// them; read() itself is only called once poll() says there is something to read.
[SupportedOSPlatform("linux")]
internal sealed unsafe partial class UnixStdinStream : Stream
{
    private const short PollIn = 0x0001;
    private const int Eintr = 4;
    private static readonly int PollSliceMilliseconds = 50;

    [StructLayout(LayoutKind.Sequential)]
    private struct PollFd
    {
        public int fd;
        public short events;
        public short revents;
    }

    private readonly int _fd;

    public UnixStdinStream(int fd = 0) => _fd = fd;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override int Read(byte[] buffer, int offset, int count) =>
        ReadCore(buffer.AsSpan(offset, count), CancellationToken.None);

    public override int Read(Span<byte> buffer) => ReadCore(buffer, CancellationToken.None);

    // The wait happens on a pool thread, so the caller's thread is never blocked. The thread is
    // only occupied while no key is pressed, in 50 ms slices — and is released promptly on cancel.
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        new(Task.Run(() => ReadCore(buffer.Span, cancellationToken), CancellationToken.None));

    private int ReadCore(Span<byte> buffer, CancellationToken ct)
    {
        if (buffer.IsEmpty)
            return 0;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var pollFd = new PollFd { fd = _fd, events = PollIn };
            var ready = poll(&pollFd, 1, PollSliceMilliseconds);
            if (ready < 0)
            {
                if (Marshal.GetLastPInvokeError() == Eintr)
                    continue; // a signal (SIGWINCH, ...) interrupted the wait — just wait again

                throw new IOException($"poll failed (errno {Marshal.GetLastPInvokeError()}).");
            }

            if (ready == 0)
                continue; // slice elapsed with no data — back to the cancellation check

            // Readable includes "hung up" (revents has POLLHUP but not POLLIN): read() then
            // returns 0, which is the end-of-stream the caller expects.
            fixed (byte* p = buffer)
            {
                var n = read(_fd, p, buffer.Length);
                if (n >= 0)
                    return (int)n;

                if (Marshal.GetLastPInvokeError() == Eintr)
                    continue;

                throw new IOException($"read failed (errno {Marshal.GetLastPInvokeError()}).");
            }
        }
    }

    [LibraryImport("libc", SetLastError = true)]
    private static partial int poll(PollFd* fds, ulong nfds, int timeout);

    [LibraryImport("libc", SetLastError = true)]
    private static partial nint read(int fd, byte* buf, nint count);
}
