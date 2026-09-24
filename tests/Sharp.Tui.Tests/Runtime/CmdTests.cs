using Sharp.Tui.Runtime;

namespace Sharp.Tui.Tests.Runtime;

public class CmdTests
{
    private record TestMsg;

    [Fact]
    public async Task None_ProducesNoMessage()
    {
        var result = await Cmd.None<TestMsg>()(CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public void None_ReturnsTheSameCachedInstanceEveryTime()
    {
        Assert.Same(Cmd.None<TestMsg>(), Cmd.None<TestMsg>());
    }

    [Fact]
    public void Quit_ReturnsTheSameCachedInstanceEveryTime()
    {
        Assert.Same(Cmd.Quit<TestMsg>(), Cmd.Quit<TestMsg>());
    }

    [Fact]
    public void IsNone_OnlyMatchesTheNoneInstance()
    {
        Cmd<TestMsg> custom = _ => Task.FromResult<TestMsg?>(null);

        Assert.True(Cmd.IsNone(Cmd.None<TestMsg>()));
        Assert.False(Cmd.IsNone(Cmd.Quit<TestMsg>()));
        Assert.False(Cmd.IsNone(custom));
    }

    [Fact]
    public void IsQuit_OnlyMatchesTheQuitInstance()
    {
        Cmd<TestMsg> custom = _ => Task.FromResult<TestMsg?>(null);

        Assert.True(Cmd.IsQuit(Cmd.Quit<TestMsg>()));
        Assert.False(Cmd.IsQuit(Cmd.None<TestMsg>()));
        Assert.False(Cmd.IsQuit(custom));
    }

    [Fact]
    public void NoneAndQuit_AreDistinctInstances()
    {
        Assert.NotSame(Cmd.None<TestMsg>(), Cmd.Quit<TestMsg>());
    }

    [Fact]
    public void Instances_AreCachedPerMessageType_NotSharedAcrossThem()
    {
        // Different closed generic types can't share a delegate instance, and each recognizes
        // only its own.
        Assert.NotSame((object)Cmd.Quit<TestMsg>(), (object)Cmd.Quit<string>());
        Assert.True(Cmd.IsQuit(Cmd.Quit<string>()));
    }
}
