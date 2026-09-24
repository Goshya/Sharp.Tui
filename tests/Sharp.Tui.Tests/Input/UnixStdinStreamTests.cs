using System.IO.Pipes;
using System.Runtime.Versioning;
using Sharp.Tui.Core.Input;

namespace Sharp.Tui.Tests.Input;

// The stream reads a raw file descriptor, so on Linux an anonymous pipe's read end (whose handle
// *is* the fd) stands in for stdin. On other platforms these tests have nothing to exercise and
// return immediately.
[SupportedOSPlatform("linux")]
public class UnixStdinStreamTests
{
    private static (AnonymousPipeServerStream Writer, AnonymousPipeClientStream Reader, UnixStdinStream Stream) CreatePipe()
    {
        var writer = new AnonymousPipeServerStream(PipeDirection.Out);
        var reader = new AnonymousPipeClientStream(PipeDirection.In, writer.ClientSafePipeHandle);
        var stream = new UnixStdinStream((int)reader.SafePipeHandle.DangerousGetHandle());
        return (writer, reader, stream);
    }

    [Fact]
    public async Task ReadAsync_ReturnsBytesAsSoonAsTheyAreWritten()
    {
        if (!OperatingSystem.IsLinux())
            return;

        var (writer, reader, stream) = CreatePipe();
        using var _ = writer;
        using var __ = reader;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        writer.Write("+"u8);
        writer.Flush();
        var buffer = new byte[16];
        var read = await stream.ReadAsync(buffer, cts.Token);

        Assert.Equal(1, read);
        Assert.Equal((byte)'+', buffer[0]);
    }

    [Fact]
    public async Task ReadAsync_NoData_CancelsPromptly()
    {
        if (!OperatingSystem.IsLinux())
            return;

        var (writer, reader, stream) = CreatePipe();
        using var _ = writer;
        using var __ = reader;
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            var read = await stream.ReadAsync(new byte[16], cts.Token);
            Assert.Fail($"Expected cancellation, read {read} bytes.");
        });
    }

    [Fact]
    public async Task ReadAsync_WriterClosed_ReturnsZero()
    {
        if (!OperatingSystem.IsLinux())
            return;

        var (writer, reader, stream) = CreatePipe();
        using var _ = reader;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        writer.Dispose();
        var read = await stream.ReadAsync(new byte[16], cts.Token);

        Assert.Equal(0, read);
    }

    [Fact]
    public void Stream_IsReadOnlyAndNonSeekable()
    {
        if (!OperatingSystem.IsLinux())
            return;

        var stream = new UnixStdinStream();

        Assert.True(stream.CanRead);
        Assert.False(stream.CanWrite);
        Assert.False(stream.CanSeek);
        Assert.Throws<NotSupportedException>(() => stream.Write(new byte[1], 0, 1));
    }
}
