using Sharp.Tui.Core.Terminal;

namespace Sharp.Tui.Tests.Terminal;

public class UnixTermiosFlagsTests
{
    [Fact]
    public void ToRawLocalFlags_ClearsIcanonAndEcho_PreservesOtherBits()
    {
        const uint unrelatedBits = 0b1000_0000_0000;
        var original = unrelatedBits | UnixTermiosFlags.Icanon | UnixTermiosFlags.Echo;

        var raw = UnixTermiosFlags.ToRawLocalFlags(original);

        Assert.Equal(0u, raw & UnixTermiosFlags.Icanon);
        Assert.Equal(0u, raw & UnixTermiosFlags.Echo);
        Assert.Equal(unrelatedBits, raw & unrelatedBits);
    }

    [Fact]
    public void ToRawLocalFlags_AlreadyMissingIcanonAndEcho_IsIdempotent()
    {
        var raw = UnixTermiosFlags.ToRawLocalFlags(originalLocalFlags: 0);

        Assert.Equal(0u, raw);
    }
}
