using System;
using System.Collections.Generic;
using System.Text;

namespace Sharp.Tui.Core.Rendering;

public enum ColorSupport : byte { NoColor, Named16, Indexed256, TrueColor }

public readonly record struct TerminalCapabilities(ColorSupport ColorSupport)
{
    public static TerminalCapabilities Detect() =>
        Detect(Environment.GetEnvironmentVariable, Console.IsOutputRedirected);

    internal static TerminalCapabilities Detect(Func<string, string?> getEnv, bool isOutputRedirected)
    {
        return new TerminalCapabilities(DetectColorSupport(getEnv, isOutputRedirected));
    }

    private static ColorSupport DetectColorSupport(Func<string, string?> getEnv, bool isOutputRedirected)
    {
        // явный opt-out — общепринятая конвенция https://no-color.org
        if (getEnv("NO_COLOR") is not null)
            return ColorSupport.NoColor;

        // вывод не в интерактивный терминал (пайп/редирект) — ANSI-коды там не нужны
        if (isOutputRedirected)
            return ColorSupport.NoColor;

        if (getEnv("COLORTERM") is "truecolor" or "24bit")
            return ColorSupport.TrueColor;

        // Windows Terminal поддерживает TrueColor при включённом VT processing
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