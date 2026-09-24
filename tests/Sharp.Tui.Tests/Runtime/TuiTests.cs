using Sharp.Tui.Core.Input;
using Sharp.Tui.Runtime;
using Sharp.Tui.Widgets;

namespace Sharp.Tui.Tests.Runtime;

// Tui.Run's real-terminal wiring can't run under `dotnet test` (that's what the samples are
// for) — these cover what can be checked without one. The loop's behaviour itself is covered by
// RuntimeLoopTests.
public class TuiTests
{
    private sealed class NoopApp : IApp<int, string>
    {
        public (int Model, Cmd<string> Cmd) Init() => (0, Cmd.None<string>());
        public (int Model, Cmd<string> Cmd) Update(int model, string msg) => (model, Cmd.None<string>());
        public Widget View(int model) => throw new NotSupportedException();
    }

    [Fact]
    public async Task RunAsync_NullApp_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            Sharp.Tui.Runtime.Tui.RunAsync<int, string>(null!, _ => null));
    }

    [Fact]
    public async Task RunAsync_NullKeyMap_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            Sharp.Tui.Runtime.Tui.RunAsync(new NoopApp(), null!));
    }

    [Fact]
    public async Task RunAsync_RedirectedStdio_FailsWithAClearMessageBeforeTouchingTheTerminal()
    {
        if (!Console.IsInputRedirected && !Console.IsOutputRedirected)
            return; // running attached to a real terminal: nothing redirected to complain about

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Sharp.Tui.Runtime.Tui.RunAsync(new NoopApp(), (KeyEvent _) => (string?)null));

        Assert.Contains("interactive terminal", ex.Message);
    }

    [Fact]
    public void Run_NullApp_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Sharp.Tui.Runtime.Tui.Run<int, string>(null!, _ => null));
    }
}
