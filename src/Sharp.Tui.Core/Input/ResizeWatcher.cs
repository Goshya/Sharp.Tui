using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Sharp.Tui.Core.Input;

// Both platforms ultimately do the same thing — read Console.WindowWidth/Height, compare to
// the last known size, emit a ResizeEvent if it changed — they only differ in *when* to check:
// Unix gets an immediate SIGWINCH signal, Windows has no such signal so falls back to polling.
public sealed class ResizeWatcher
{
    private readonly Func<(int Width, int Height)> _getSize;
    private readonly TimeSpan _pollInterval;
    private readonly TimeProvider _timeProvider;

    public ResizeWatcher(Func<(int Width, int Height)>? getSize = null, TimeSpan? pollInterval = null, TimeProvider? timeProvider = null)
    {
        _getSize = getSize ?? (() => (Console.WindowWidth, Console.WindowHeight));
        _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(100);
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    // PosixSignalRegistration is a portable BCL abstraction (unlike our raw termios P/Invoke in
    // M1.4) — SIGWINCH genuinely works the same way on Linux, macOS and FreeBSD, so unlike
    // UnixRawModeToggle there's no "can't verify macOS" gap here.
    public IAsyncEnumerable<InputEvent> WatchAsync(CancellationToken ct = default) =>
        OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() || OperatingSystem.IsFreeBSD()
            ? WatchViaSignalAsync(ct)
            : WatchViaPollingAsync(ct);

    // internal (not private) so tests can exercise this path deterministically regardless of
    // which OS the test suite happens to run on — WatchAsync's own dispatch would otherwise
    // pick the SIGWINCH path on Linux/macOS CI, leaving this one untested there.
    internal async IAsyncEnumerable<InputEvent> WatchViaPollingAsync([EnumeratorCancellation] CancellationToken ct)
    {
        var (lastWidth, lastHeight) = _getSize();

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(_pollInterval, _timeProvider, ct).ConfigureAwait(false);

            var (width, height) = _getSize();
            if (width == lastWidth && height == lastHeight)
                continue;

            lastWidth = width;
            lastHeight = height;
            yield return InputEvent.Resize(new ResizeEvent(width, height));
        }
    }

    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    [SupportedOSPlatform("freebsd")]
    private async IAsyncEnumerable<InputEvent> WatchViaSignalAsync([EnumeratorCancellation] CancellationToken ct)
    {
        var (lastWidth, lastHeight) = _getSize();
        var signalled = Channel.CreateUnbounded<bool>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });

        using var registration = PosixSignalRegistration.Create(PosixSignal.SIGWINCH, context =>
        {
            context.Cancel = true; // SIGWINCH's default disposition is "ignore" anyway, but be explicit
            signalled.Writer.TryWrite(true);
        });

        while (await signalled.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
        {
            // A burst of SIGWINCH can arrive while a window is actively being dragged —
            // coalesce it into a single check of the size it eventually settled on, rather
            // than emitting one ResizeEvent per signal.
            while (signalled.Reader.TryRead(out _))
            {
            }

            var (width, height) = _getSize();
            if (width == lastWidth && height == lastHeight)
                continue;

            lastWidth = width;
            lastHeight = height;
            yield return InputEvent.Resize(new ResizeEvent(width, height));
        }
    }
}
