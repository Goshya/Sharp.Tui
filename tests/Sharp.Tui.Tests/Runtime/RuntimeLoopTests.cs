using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using Sharp.Tui.Core.Input;
using Sharp.Tui.Core.Rendering;
using Sharp.Tui.Layout;
using Sharp.Tui.Runtime;
using Sharp.Tui.Widgets;
using Buffer = Sharp.Tui.Core.Rendering.Buffer;

namespace Sharp.Tui.Tests.Runtime;

public class RuntimeLoopTests
{
    private abstract record Msg;
    private sealed record Inc : Msg;
    private sealed record Dec : Msg;
    private sealed record QuitRequested : Msg;
    private sealed record Loaded(int Value) : Msg;
    private sealed record Resized(int Width) : Msg;

    private sealed record Model(int Count);

    // Draws its text on the first row of whatever area it's given — the smallest possible widget.
    private sealed record TextWidget(string Text) : Widget
    {
        public override void Render(Buffer buffer, Rect area)
        {
            for (var i = 0; i < Text.Length && i < area.Width; i++)
                buffer[area.X + i, area.Y] = new Cell(new Rune(Text[i]), Color.Default, Color.Default, StyleFlags.None);
        }

        // Not exercised by any RuntimeLoop test — only present to satisfy the abstract member.
        public override Size Measure(Constraints constraints) => new(Text.Length, 1);
    }

    private sealed class LambdaApp(
        Func<(Model, Cmd<Msg>)> init,
        Func<Model, Msg, (Model, Cmd<Msg>)> update,
        Func<Model, Widget> view) : IApp<Model, Msg>
    {
        public (Model Model, Cmd<Msg> Cmd) Init() => init();
        public (Model Model, Cmd<Msg> Cmd) Update(Model model, Msg msg) => update(model, msg);
        public Widget View(Model model) => view(model);
    }

    // The counter from docs/SPEC.md §3, plus a record of every message Update saw.
    private static LambdaApp CounterApp(List<Msg>? received = null, Func<Model, Msg, (Model, Cmd<Msg>)>? extraUpdate = null) => new(
        init: () => (new Model(0), Cmd.None<Msg>()),
        update: (model, msg) =>
        {
            received?.Add(msg);
            if (extraUpdate is not null)
                return extraUpdate(model, msg);

            return msg switch
            {
                Inc => (model with { Count = model.Count + 1 }, Cmd.None<Msg>()),
                Dec => (model with { Count = model.Count - 1 }, Cmd.None<Msg>()),
                QuitRequested => (model, Cmd.Quit<Msg>()),
                Loaded loaded => (model with { Count = loaded.Value }, Cmd.None<Msg>()),
                Resized resized => (model with { Count = resized.Width }, Cmd.None<Msg>()),
                _ => (model, Cmd.None<Msg>()),
            };
        },
        view: model => new TextWidget($"Count: {model.Count}"));

    private static Msg? CounterKeys(KeyEvent key) => key.Char.Value switch
    {
        '+' => new Inc(),
        '-' => new Dec(),
        'q' => new QuitRequested(),
        _ => null,
    };

    private sealed class Harness : IAsyncDisposable
    {
        private readonly CancellationTokenSource _cts = new();

        public VirtualScreenWriter Screen { get; }
        public Channel<InputEvent> Input { get; } = Channel.CreateUnbounded<InputEvent>();
        public RuntimeLoop<Model, Msg> Loop { get; }
        public Task Run { get; }

        public Harness(
            IApp<Model, Msg> app,
            Func<KeyEvent, Msg?>? keyMap = null,
            int width = 40,
            int height = 3,
            int screenWidth = 40,
            Func<ResizeEvent, Msg?>? resizeMap = null,
            IReadOnlyList<Sub<Msg>>? subscriptions = null)
        {
            Screen = new VirtualScreenWriter(screenWidth, height);
            Loop = new RuntimeLoop<Model, Msg>(
                app, Screen, width, height,
                [Input.Reader.ReadAllAsync()],
                keyMap ?? CounterKeys, resizeMap, subscriptions);
            Run = Loop.RunAsync(_cts.Token);
        }

        public void Press(char c) => Input.Writer.TryWrite(InputEvent.Key(KeyEvent.FromChar(new Rune(c))));

        public void Cancel() => _cts.Cancel();

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            try { await Run; } catch { /* the test that cares about a failure awaits Run itself */ }
            _cts.Dispose();
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition, string description)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"Timed out waiting for: {description}");

            await Task.Delay(5);
        }
    }

    [Fact]
    public async Task Run_RendersTheInitialModel()
    {
        await using var harness = new Harness(CounterApp());

        Assert.Equal("Count: 0", harness.Screen.Row(0));
    }

    [Fact]
    public async Task Run_KeyPress_GoesThroughKeyMapAndUpdateAndIsRenderedAgain()
    {
        await using var harness = new Harness(CounterApp());

        harness.Press('+');
        harness.Press('+');
        harness.Press('-');
        harness.Press('+');

        await WaitUntilAsync(() => harness.Screen.Row(0) == "Count: 2", "Count: 2 on screen");
    }

    [Fact]
    public async Task Run_KeyMapReturningNull_DoesNotCallUpdate()
    {
        var received = new List<Msg>();
        await using var harness = new Harness(CounterApp(received));

        harness.Press('z'); // not mapped to anything
        harness.Press('+');

        await WaitUntilAsync(() => harness.Screen.Row(0) == "Count: 1", "Count: 1 on screen");
        Assert.Equal([new Inc()], received);
    }

    [Fact]
    public async Task Run_UpdateReturningQuit_EndsTheLoop()
    {
        await using var harness = new Harness(CounterApp());

        harness.Press('q');

        await harness.Run.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Run_InitReturningQuit_EndsWithoutEverRendering()
    {
        var app = new LambdaApp(
            init: () => (new Model(0), Cmd.Quit<Msg>()),
            update: (model, _) => (model, Cmd.None<Msg>()),
            view: _ => new TextWidget("never drawn"));
        await using var harness = new Harness(app);

        await harness.Run.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(0, harness.Screen.FlushCount);
        Assert.Equal("", harness.Screen.Row(0));
    }

    [Fact]
    public async Task Run_CmdFromUpdate_ResultReentersTheLoopAsAMessage()
    {
        var app = CounterApp(extraUpdate: (model, msg) => msg switch
        {
            Inc => (model, (Cmd<Msg>)(_ => Task.FromResult<Msg?>(new Loaded(42)))),
            Loaded loaded => (model with { Count = loaded.Value }, Cmd.None<Msg>()),
            _ => (model, Cmd.None<Msg>()),
        });
        await using var harness = new Harness(app);

        harness.Press('+');

        await WaitUntilAsync(() => harness.Screen.Row(0) == "Count: 42", "the Cmd's message to arrive");
    }

    [Fact]
    public async Task Run_CmdFromInit_IsDispatched()
    {
        var app = new LambdaApp(
            init: () => (new Model(0), _ => Task.FromResult<Msg?>(new Loaded(7))),
            update: (model, msg) => msg is Loaded l ? (model with { Count = l.Value }, Cmd.None<Msg>()) : (model, Cmd.None<Msg>()),
            view: model => new TextWidget($"Count: {model.Count}"));
        await using var harness = new Harness(app);

        await WaitUntilAsync(() => harness.Screen.Row(0) == "Count: 7", "the Init Cmd's message to arrive");
    }

    [Fact]
    public async Task Run_CmdReturningNull_ProducesNoMessage()
    {
        var received = new List<Msg>();
        var app = CounterApp(received, (model, msg) => msg is Inc
            ? (model, (Cmd<Msg>)(_ => Task.FromResult<Msg?>(null)))
            : (model, Cmd.None<Msg>()));
        await using var harness = new Harness(app);

        harness.Press('+');
        await WaitUntilAsync(() => received.Count == 1, "Update to see the key");
        await Task.Delay(50);

        Assert.Equal([new Inc()], received);
    }

    [Fact]
    public async Task Run_SubscriptionMessages_FlowThroughUpdate()
    {
        static async IAsyncEnumerable<Msg> ThreeIncs([EnumeratorCancellation] CancellationToken ct)
        {
            for (var i = 0; i < 3; i++)
            {
                yield return new Inc();
                await Task.Yield();
            }
        }

        await using var harness = new Harness(CounterApp(), subscriptions: [ThreeIncs]);

        await WaitUntilAsync(() => harness.Screen.Row(0) == "Count: 3", "three ticks from the Sub");
    }

    [Fact]
    public async Task Run_TickSub_DeliversRepeatedMessages()
    {
        var sub = Sub.Tick<Msg>(TimeSpan.FromMilliseconds(10), _ => new Inc());
        await using var harness = new Harness(CounterApp(), subscriptions: [sub]);

        await WaitUntilAsync(
            () => harness.Screen.Row(0) is { } row && row.StartsWith("Count: ") && int.Parse(row["Count: ".Length..]) >= 3,
            "at least three Tick messages");
    }

    [Fact]
    public async Task Run_ResizeEvent_ResizesTheBufferAndRepaintsInFull()
    {
        var app = new LambdaApp(
            init: () => (new Model(0), Cmd.None<Msg>()),
            update: (model, _) => (model, Cmd.None<Msg>()),
            view: _ => new TextWidget("0123456789ABCDEFGHIJ"));
        await using var harness = new Harness(app, width: 10, screenWidth: 40);
        Assert.Equal("0123456789", harness.Screen.Row(0)); // the 10-wide buffer clips the widget

        harness.Input.Writer.TryWrite(InputEvent.Resize(new ResizeEvent(20, 3)));

        await WaitUntilAsync(() => harness.Screen.Row(0) == "0123456789ABCDEFGHIJ", "the widget repainted at width 20");
    }

    [Fact]
    public async Task Run_ResizeMap_MessageReachesUpdate()
    {
        await using var harness = new Harness(CounterApp(), resizeMap: r => new Resized(r.Width));

        harness.Input.Writer.TryWrite(InputEvent.Resize(new ResizeEvent(33, 3)));

        await WaitUntilAsync(() => harness.Screen.Row(0) == "Count: 33", "Update to see the resize message");
    }

    [Fact]
    public async Task Run_MouseAndPasteEvents_AreIgnoredWithoutStoppingTheLoop()
    {
        await using var harness = new Harness(CounterApp());

        harness.Input.Writer.TryWrite(InputEvent.Mouse(new MouseEvent(1, 1, MouseButton.Left, MouseAction.Down)));
        harness.Input.Writer.TryWrite(InputEvent.Paste("pasted"));
        harness.Press('+');

        await WaitUntilAsync(() => harness.Screen.Row(0) == "Count: 1", "the loop to still process keys");
    }

    [Fact]
    public async Task Run_ExternalCancellation_EndsNormallyAndCancelsInFlightCmds()
    {
        var started = new TaskCompletionSource();
        var observedCancellation = new TaskCompletionSource();
        var app = CounterApp(extraUpdate: (model, msg) => msg is Inc
            ? (model, (Cmd<Msg>)(async ct =>
            {
                started.SetResult();
                try
                {
                    await Task.Delay(Timeout.Infinite, ct);
                }
                catch (OperationCanceledException)
                {
                    observedCancellation.SetResult();
                    throw;
                }

                return null;
            }))
            : (model, Cmd.None<Msg>()));
        await using var harness = new Harness(app);

        harness.Press('+');
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        harness.Cancel();

        await harness.Run.WaitAsync(TimeSpan.FromSeconds(5)); // completes, doesn't throw
        Assert.True(observedCancellation.Task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Run_ExceptionInCmd_PropagatesOutOfRunAsync()
    {
        var app = CounterApp(extraUpdate: (model, msg) => msg is Inc
            ? (model, (Cmd<Msg>)(_ => throw new InvalidOperationException("boom")))
            : (model, Cmd.None<Msg>()));
        await using var harness = new Harness(app);

        harness.Press('+');

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Run.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal("boom", ex.Message);
    }

    [Fact]
    public async Task Run_ExceptionInUpdate_PropagatesOutOfRunAsync()
    {
        var app = CounterApp(extraUpdate: (_, _) => throw new InvalidOperationException("update failed"));
        await using var harness = new Harness(app);

        harness.Press('+');

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Run.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal("update failed", ex.Message);
    }

    [Fact]
    public async Task Run_CalledTwice_Throws()
    {
        await using var harness = new Harness(CounterApp());

        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Loop.RunAsync());
    }

    [Fact]
    public void Constructor_NullArguments_Throw()
    {
        var writer = new VirtualScreenWriter(1, 1);
        var app = CounterApp();

        Assert.Throws<ArgumentNullException>(() => new RuntimeLoop<Model, Msg>(null!, writer, 1, 1, [], CounterKeys));
        Assert.Throws<ArgumentNullException>(() => new RuntimeLoop<Model, Msg>(app, null!, 1, 1, [], CounterKeys));
        Assert.Throws<ArgumentNullException>(() => new RuntimeLoop<Model, Msg>(app, writer, 1, 1, null!, CounterKeys));
        Assert.Throws<ArgumentNullException>(() => new RuntimeLoop<Model, Msg>(app, writer, 1, 1, [], null!));
    }
}
