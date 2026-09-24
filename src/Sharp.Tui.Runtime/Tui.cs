using Sharp.Tui.Core.Input;
using Sharp.Tui.Core.Rendering;
using Sharp.Tui.Core.Terminal;

namespace Sharp.Tui.Runtime;

// The public entry point, and the one place that touches the real terminal: everything else
// (RuntimeLoop and the M1 pieces it drives) is injected/tested without one. The app author never
// sees AnsiTerminalWriter, DiffRenderer, IRawModeScope or InputReader — only this.
public static class Tui
{
    // Blocking form, so a Main can just call Tui.Run(app, keyMap) like the example in
    // docs/SPEC.md §3. Safe to block on: a console app has no SynchronizationContext to deadlock.
    public static void Run<TModel, TMsg>(
        IApp<TModel, TMsg> app,
        Func<KeyEvent, TMsg?> keyMap,
        Func<ResizeEvent, TMsg?>? resizeMap = null,
        IReadOnlyList<Sub<TMsg>>? subscriptions = null) =>
        RunAsync(app, keyMap, resizeMap, subscriptions).GetAwaiter().GetResult();

    // Returns when the app quits (Cmd.Quit), Ctrl+C is pressed, or `ct` is cancelled; an unhandled
    // exception from the app propagates out — after the terminal has been restored.
    public static async Task RunAsync<TModel, TMsg>(
        IApp<TModel, TMsg> app,
        Func<KeyEvent, TMsg?> keyMap,
        Func<ResizeEvent, TMsg?>? resizeMap = null,
        IReadOnlyList<Sub<TMsg>>? subscriptions = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(keyMap);

        if (Console.IsInputRedirected || Console.IsOutputRedirected)
            throw new InvalidOperationException(
                "Sharp.Tui needs an interactive terminal: standard input and output must not be redirected.");

        var capabilities = TerminalCapabilities.Detect();
        var writer = new AnsiTerminalWriter(Console.OpenStandardOutput(), capabilities.ColorSupport);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        // Ctrl+C is a request to exit, not a reason to kill the process mid-frame: cancelling the
        // default action lets the loop wind down and the scope below restore the terminal itself.
        ConsoleCancelEventHandler onCancelKeyPress = (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };
        Console.CancelKeyPress += onCancelKeyPress;

        try
        {
            using var rawMode = RawMode.Enter(writer);

            var loop = new RuntimeLoop<TModel, TMsg>(
                app,
                writer,
                Console.WindowWidth,
                Console.WindowHeight,
                [new InputReader(InputReader.OpenStandardInput()).ReadAsync(), new ResizeWatcher().WatchAsync()],
                keyMap,
                resizeMap,
                subscriptions);

            await loop.RunAsync(cts.Token).ConfigureAwait(false);
        }
        finally
        {
            Console.CancelKeyPress -= onCancelKeyPress;
        }
    }
}
