namespace Sharp.Tui.Core.Rendering;

internal static class ColorConversion
{
    // xterm's default RGB values for the 16 ANSI colors; real terminals vary by theme, so this is an approximation.
    private static ReadOnlySpan<byte> Named16Rgb =>
    [
        0, 0, 0,
        205, 0, 0,
        0, 205, 0,
        205, 205, 0,
        0, 0, 238,
        205, 0, 205,
        0, 205, 205,
        229, 229, 229,
        127, 127, 127,
        255, 0, 0,
        0, 255, 0,
        255, 255, 0,
        92, 92, 255,
        255, 0, 255,
        0, 255, 255,
        255, 255, 255,
    ];

    internal static byte RgbToNamed16(byte r, byte g, byte b)
    {
        var palette = Named16Rgb;
        var best = 0;
        var bestDistance = int.MaxValue;

        for (var i = 0; i < 16; i++)
        {
            var distance = DistanceSquared(r, g, b, palette[i * 3], palette[i * 3 + 1], palette[i * 3 + 2]);
            if (distance < bestDistance)
            {
                best = i;
                bestDistance = distance;
            }
        }

        return (byte)best;
    }

    internal static byte Indexed256ToNamed16(byte index)
    {
        if (index < 16)
            return index;

        var (r, g, b) = Indexed256ToRgb(index);
        return RgbToNamed16(r, g, b);
    }

    // Indices 0-15 are left alone: their actual color depends on the terminal theme.
    // Only the 6x6x6 cube (16-231) and the grayscale ramp (232-255) are candidates.
    internal static byte RgbToIndexed256(byte r, byte g, byte b)
    {
        var ri = CubeIndex(r);
        var gi = CubeIndex(g);
        var bi = CubeIndex(b);
        var cubeDistance = DistanceSquared(r, g, b, CubeLevel(ri), CubeLevel(gi), CubeLevel(bi));

        var average = (r + g + b) / 3;
        var greyStep = Math.Clamp((average - 3) / 10, 0, 23);
        var grey = 8 + 10 * greyStep;
        var greyDistance = DistanceSquared(r, g, b, grey, grey, grey);

        return greyDistance < cubeDistance
            ? (byte)(232 + greyStep)
            : (byte)(16 + 36 * ri + 6 * gi + bi);
    }

    private static (byte R, byte G, byte B) Indexed256ToRgb(byte index)
    {
        if (index >= 232)
        {
            var grey = (byte)(8 + 10 * (index - 232));
            return (grey, grey, grey);
        }

        var cube = index - 16;
        return (CubeLevel(cube / 36), CubeLevel(cube / 6 % 6), CubeLevel(cube % 6));
    }

    // The cube's per-channel levels are 0, 95, 135, 175, 215, 255.
    private static byte CubeLevel(int level) => (byte)(level == 0 ? 0 : 55 + 40 * level);

    private static int CubeIndex(byte channel) => channel < 48 ? 0 : channel < 115 ? 1 : (channel - 35) / 40;

    private static int DistanceSquared(int r1, int g1, int b1, int r2, int g2, int b2)
    {
        var dr = r1 - r2;
        var dg = g1 - g2;
        var db = b1 - b2;
        return dr * dr + dg * dg + db * db;
    }
}
