using System.Collections.Concurrent;
using System.Threading.Channels;
using Sharp.Tui.Core.Input;
using Sharp.Tui.Core.Rendering;
using Sharp.Tui.Core.Terminal;
using Sharp.Tui.Layout;
using Buffer = Sharp.Tui.Core.Rendering.Buffer;

namespace Sharp.Tui.Runtime;

// The Elm loop itself, with every real-world dependency injected (writer, input sources, initial
// size) so it can be driven by tests without a terminal. Tui.Run is the thin wrapper that
// wires up the real Console I/O.
//
// Threading rule: all user code (Init/Update/View/keyMap/resizeMap) runs on the loop's own
// continuation only. Background tasks (input pumps, Cmds, Subs) do nothing but post items to a
// single channel — a KeyEvent is turned into a TMsg by the loop, not by the pump that read it.
internal sealed class RuntimeLoop<TModel, TMsg>
{
    private const int MaxMessagesPerFrame = 64;

    private enum ItemKind { Message, Key, Resize }

    private readonly record struct Item(ItemKind Kind, TMsg? Message = default, KeyEvent Key = default, ResizeEvent Resize = default);

    private readonly IApp<TModel, TMsg> _app;
    private readonly ITerminalWriter _writer;
    private readonly IReadOnlyList<IAsyncEnumerable<InputEvent>> _inputSources;
    private readonly Func<KeyEvent, TMsg?> _keyMap;
    private readonly Func<ResizeEvent, TMsg?>? _resizeMap;
    private readonly IReadOnlyList<Sub<TMsg>> _subscriptions;

    private readonly DiffRenderer _renderer = new();
    private readonly Channel<Item> _channel = Channel.CreateUnbounded<Item>(new UnboundedChannelOptions { SingleReader = true });
    private readonly ConcurrentDictionary<int, Task> _inFlight = new();

    private Buffer _back;
    private int _nextTaskId;
    private int _started;
    private bool _quitRequested;

    public RuntimeLoop(
        IApp<TModel, TMsg> app,
        ITerminalWriter writer,
        int width,
        int height,
        IReadOnlyList<IAsyncEnumerable<InputEvent>> inputSources,
        Func<KeyEvent, TMsg?> keyMap,
        Func<ResizeEvent, TMsg?>? resizeMap = null,
        IReadOnlyList<Sub<TMsg>>? subscriptions = null)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(inputSources);
        ArgumentNullException.ThrowIfNull(keyMap);

        _app = app;
        _writer = writer;
        _inputSources = inputSources;
        _keyMap = keyMap;
        _resizeMap = resizeMap;
        _subscriptions = subscriptions ?? [];
        _back = new Buffer(width, height);
    }

    // Runs until Update/Init returns Cmd.Quit, an unhandled exception occurs (it propagates from
    // here), or `ct` is cancelled — external cancellation is a normal shutdown, not an error.
    // Either way, in-flight Cmds/Subs/pumps are cancelled and awaited before this returns.
    public async Task RunAsync(CancellationToken ct = default)
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
            throw new InvalidOperationException("A RuntimeLoop can only be run once.");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = cts.Token;

        try
        {
            var (model, cmd) = _app.Init();
            Dispatch(cmd, token);
            if (_quitRequested)
                return;

            Render(model);

            foreach (var source in _inputSources)
                Track(Task.Run(() => PumpInputAsync(source, token), CancellationToken.None));
            foreach (var sub in _subscriptions)
                Track(Task.Run(() => PumpSubAsync(sub, token), CancellationToken.None));

            while (await _channel.Reader.WaitToReadAsync(token).ConfigureAwait(false))
            {
                var dirty = false;

                // One render per batch of already-queued items rather than one per item, capped
                // so a flood of events can't starve the render itself.
                for (var n = 0; n < MaxMessagesPerFrame && _channel.Reader.TryRead(out var item); n++)
                {
                    model = Process(item, model, token, ref dirty);
                    if (_quitRequested)
                        return;
                }

                if (dirty)
                    Render(model);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        finally
        {
            cts.Cancel();
            await Task.WhenAll(_inFlight.Values).ConfigureAwait(false);
        }
    }

    private TModel Process(Item item, TModel model, CancellationToken token, ref bool dirty)
    {
        switch (item.Kind)
        {
            case ItemKind.Message:
                dirty = true;
                return Apply(model, item.Message!, token);

            case ItemKind.Key:
                var keyMessage = _keyMap(item.Key);
                if (keyMessage is null)
                    return model;

                dirty = true;
                return Apply(model, keyMessage, token);

            case ItemKind.Resize:
                // Not left to every app to remember: a new size always means a new back buffer
                // and a full repaint (the terminal may have reflowed whatever was on screen).
                _back = new Buffer(item.Resize.Width, item.Resize.Height);
                _renderer.Invalidate();
                dirty = true;

                var resizeMessage = _resizeMap is null ? default : _resizeMap(item.Resize);
                return resizeMessage is null ? model : Apply(model, resizeMessage, token);

            default:
                return model;
        }
    }

    private TModel Apply(TModel model, TMsg message, CancellationToken token)
    {
        var (next, cmd) = _app.Update(model, message);
        Dispatch(cmd, token);
        return next;
    }

    private void Dispatch(Cmd<TMsg> cmd, CancellationToken token)
    {
        if (cmd is null || Cmd.IsNone(cmd))
            return;

        if (Cmd.IsQuit(cmd))
        {
            _quitRequested = true;
            return;
        }

        Track(Task.Run(() => RunCmdAsync(cmd, token), CancellationToken.None));
    }

    private void Render(TModel model)
    {
        var widget = _app.View(model);
        _back.Clear();
        widget.Render(_back, new Rect(0, 0, _back.Width, _back.Height));
        _renderer.Render(_back, _writer);
    }

    private async Task RunCmdAsync(Cmd<TMsg> cmd, CancellationToken token)
    {
        try
        {
            var message = await cmd(token).ConfigureAwait(false);
            if (message is not null)
                _channel.Writer.TryWrite(new Item(ItemKind.Message, message));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
    }

    private async Task PumpSubAsync(Sub<TMsg> sub, CancellationToken token)
    {
        try
        {
            await foreach (var message in sub(token).WithCancellation(token).ConfigureAwait(false))
            {
                if (message is not null)
                    _channel.Writer.TryWrite(new Item(ItemKind.Message, message));
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
    }

    // Mouse and paste events are read but dropped for now — there is no mapping hook for them
    // yet (only keyMap/resizeMap exist), and inventing one is a public-API decision for later.
    private async Task PumpInputAsync(IAsyncEnumerable<InputEvent> source, CancellationToken token)
    {
        try
        {
            await foreach (var evt in source.WithCancellation(token).ConfigureAwait(false))
            {
                switch (evt.Kind)
                {
                    case InputEventKind.Key:
                        _channel.Writer.TryWrite(new Item(ItemKind.Key, Key: evt.AsKey));
                        break;
                    case InputEventKind.Resize:
                        _channel.Writer.TryWrite(new Item(ItemKind.Resize, Resize: evt.AsResize));
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
    }

    // A failed Cmd/Sub/input source is not swallowed: completing the channel with the exception
    // makes the loop's next read rethrow it out of RunAsync (after which the finally block still
    // cancels and drains everything, and the caller's RawMode scope restores the terminal).
    private void Fail(Exception ex) => _channel.Writer.TryComplete(ex);

    private void Track(Task task)
    {
        var id = Interlocked.Increment(ref _nextTaskId);
        _inFlight[id] = task;
        _ = task.ContinueWith(_ => { _inFlight.TryRemove(id, out Task? _); }, TaskScheduler.Default);
    }
}
