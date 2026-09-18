namespace Sharp.Tui.Core.Rendering;

public readonly record struct TerminalCapabilities(ColorSupport ColorSupport)
{
    public static TerminalCapabilities Detect() =>
        Detect(Environment.GetEnvironmentVariable, Console.IsOutputRedirected);

    internal static TerminalCapabilities Detect(Func<string, string?> getEnv, bool isOutputRedirected) =>
        new(DetectColorSupport(getEnv, isOutputRedirected));

    private static ColorSupport DetectColorSupport(Func<string, string?> getEnv, bool isOutputRedirected)
    {
        // Explicit opt-out convention: https://no-color.org
        if (getEnv("NO_COLOR") is not null)
            return ColorSupport.NoColor;

        // Output isn't attached to an interactive terminal (piped/redirected) — ANSI codes don't apply.
        if (isOutputRedirected)
            return ColorSupport.NoColor;

        if (getEnv("COLORTERM") is "truecolor" or "24bit")
            return ColorSupport.TrueColor;

        // Windows Terminal supports TrueColor whenever VT processing is enabled.
        if (getEnv("WT_SESSION") is not null)
            return ColorSupport.TrueColor;

        var term = getEnv("TERM") ?? string.Empty;
        if (term.Contains("256color", StringComparison.OrdinalIgnoreCase))
            return ColorSupport.Indexed256;

        if (term is "" or "dumb")
            return ColorSupport.NoColor;

        return ColorSupport.Named16;
    }
}
