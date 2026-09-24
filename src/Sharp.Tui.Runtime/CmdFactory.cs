namespace Sharp.Tui.Runtime;

// Cmd<TMsg> is a delegate, and delegates can't have static members — so the "built-ins" live on
// this same-named non-generic class instead (the way Task/Task<T> coexist): Cmd.None<TMsg>(),
// Cmd.Quit<TMsg>().
public static class Cmd
{
    public static Cmd<TMsg> None<TMsg>() => NoneCmd<TMsg>.Instance;

    // Quit is an effect Update returns, not a message keyMap produces: whether to exit is a
    // decision for Update (after a confirm dialog, on a fatal Cmd error, ...), not for a key table.
    public static Cmd<TMsg> Quit<TMsg>() => QuitCmd<TMsg>.Instance;

    // Reference equality, deliberately not ==: delegate == compares invocation lists, and these
    // must only ever match the exact cached instances handed out above.
    internal static bool IsNone<TMsg>(Cmd<TMsg> cmd) => ReferenceEquals(cmd, NoneCmd<TMsg>.Instance);

    internal static bool IsQuit<TMsg>(Cmd<TMsg> cmd) => ReferenceEquals(cmd, QuitCmd<TMsg>.Instance);
}

internal static class NoneCmd<TMsg>
{
    public static readonly Cmd<TMsg> Instance = static _ => Task.FromResult<TMsg?>(default);
}

internal static class QuitCmd<TMsg>
{
    // Never actually invoked by the runtime (it recognizes this instance and stops instead of
    // dispatching it); harmless no-message result if someone invokes it directly anyway.
    public static readonly Cmd<TMsg> Instance = static _ => Task.FromResult<TMsg?>(default);
}
