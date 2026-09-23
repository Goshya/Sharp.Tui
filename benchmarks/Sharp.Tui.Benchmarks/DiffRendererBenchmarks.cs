using System;
using System.Text;
using BenchmarkDotNet.Attributes;
using Sharp.Tui.Core.Rendering;
using Sharp.Tui.Core.Terminal;
using Buffer = Sharp.Tui.Core.Rendering.Buffer;

namespace Sharp.Tui.Benchmarks;

// The M1.3 budget from docs/SPEC.md §6: diffing and flushing a 200x50 buffer must stay well
// under 2ms. [MemoryDiagnoser] is along for the ride, not the pass/fail metric here — it's
// useful to see whether FrameDiff's per-call `List<DiffRun>` shows up as real GC pressure.
[MemoryDiagnoser]
public class DiffRendererBenchmarks
{
    private const int Width = 200;
    private const int Height = 50;

    private ITerminalWriter _writer = null!;

    // Each scenario gets its own DiffRenderer so the four benchmarks can't leak state into
    // each other regardless of the order BenchmarkDotNet happens to run them in.
    private DiffRenderer _invalidationRenderer = null!;
    private Buffer _content = null!;

    private DiffRenderer _steadyStateRenderer = null!;

    private DiffRenderer _smallChangeRenderer = null!;
    private Buffer _baseline = null!;
    private Buffer _withHighlightedRow = null!;
    private bool _smallChangeToggle;

    private DiffRenderer _scatteredChangeRenderer = null!;
    private Buffer _scatteredA = null!;
    private Buffer _scatteredB = null!;
    private bool _scatteredToggle;

    [GlobalSetup]
    public void Setup()
    {
        // A real sink (real ANSI byte emission, real Flush), just pointed at nowhere — this
        // measures the whole "diff + write" pipeline the budget talks about, not diffing alone.
        _writer = new AnsiTerminalWriter(System.IO.Stream.Null, ColorSupport.TrueColor);

        _content = RandomContent(seed: 1);
        _invalidationRenderer = new DiffRenderer();

        _steadyStateRenderer = new DiffRenderer();
        _steadyStateRenderer.Render(_content, _writer); // seed _front; every measured call is then a no-op diff

        _baseline = RandomContent(seed: 2);
        _withHighlightedRow = Clone(_baseline);
        HighlightRow(_withHighlightedRow, row: Height / 2);
        _smallChangeRenderer = new DiffRenderer();
        _smallChangeRenderer.Render(_baseline, _writer);

        _scatteredA = RandomContent(seed: 3);
        _scatteredB = Clone(_scatteredA);
        ScatterChanges(_scatteredB, fraction: 0.10, seed: 4);
        _scatteredChangeRenderer = new DiffRenderer();
        _scatteredChangeRenderer.Render(_scatteredA, _writer);
    }

    [Benchmark(Description = "Full invalidation (first frame / resize)")]
    public void FullInvalidation()
    {
        // Forced every call, not just once — this is the "front can't be trusted" worst case,
        // not a one-time cold start, so it has to stay a full repaint on every invocation.
        _invalidationRenderer.Invalidate();
        _invalidationRenderer.Render(_content, _writer);
    }

    [Benchmark(Description = "Steady state, nothing changed")]
    public void NoChanges() => _steadyStateRenderer.Render(_content, _writer);

    [Benchmark(Description = "Small localized change (one highlighted row)")]
    public void SmallChange()
    {
        // Toggle between two known buffers rather than always rendering the same "changed"
        // buffer — otherwise only the first call would actually diff; every call after that
        // would find _front already equal to it and measure the no-op path instead.
        _smallChangeToggle = !_smallChangeToggle;
        _smallChangeRenderer.Render(_smallChangeToggle ? _withHighlightedRow : _baseline, _writer);
    }

    [Benchmark(Description = "Scattered change (~10% of cells)")]
    public void ScatteredChange()
    {
        _scatteredToggle = !_scatteredToggle;
        _scatteredChangeRenderer.Render(_scatteredToggle ? _scatteredB : _scatteredA, _writer);
    }

    private static Buffer RandomContent(int seed)
    {
        var buffer = new Buffer(Width, Height);
        var random = new Random(seed);

        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
        {
            var ch = (char)('a' + random.Next(26));
            buffer[x, y] = new Cell(new Rune(ch), Color.Named((byte)random.Next(8)), Color.Default, StyleFlags.None);
        }

        return buffer;
    }

    private static Buffer Clone(Buffer source)
    {
        var clone = new Buffer(source.Width, source.Height);
        for (var y = 0; y < source.Height; y++)
        for (var x = 0; x < source.Width; x++)
            clone[x, y] = source[x, y];

        return clone;
    }

    private static void HighlightRow(Buffer buffer, int row)
    {
        for (var x = 0; x < buffer.Width; x++)
        {
            var current = buffer[x, row];
            buffer[x, row] = new Cell(current.Char, Color.Rgb(0, 0, 0), Color.Rgb(255, 255, 0), StyleFlags.Bold);
        }
    }

    private static void ScatterChanges(Buffer buffer, double fraction, int seed)
    {
        var random = new Random(seed);
        var count = (int)(buffer.Width * buffer.Height * fraction);

        for (var i = 0; i < count; i++)
        {
            var x = random.Next(buffer.Width);
            var y = random.Next(buffer.Height);
            buffer[x, y] = new Cell(new Rune('#'), Color.Rgb(0, 255, 0), Color.Default, StyleFlags.None);
        }
    }
}
