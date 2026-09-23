using System;
using System.Collections.Generic;
using System.Text;
using Sharp.Tui.Core.Terminal;

namespace Sharp.Tui.Core.Rendering;

// Orchestrator: owns the previously-flushed frame (_front), asks FrameDiff which column
// ranges changed against the freshly-rendered one (back), and translates only those ranges
// into ITerminalWriter calls. Diffing itself (FrameDiff) knows nothing about ITerminalWriter;
// this is the one place the two sides meet.
public sealed class DiffRenderer
{
    private Buffer? _front;

    // Set by Invalidate() and consumed by the next Render only — keeps _front itself alive so
    // CommitFront can still reuse its array via CopyFrom when the size hasn't actually changed,
    // instead of discarding and reallocating it just because *some* invalidation happened.
    private bool _forceFullRepaint;

    // Reused across every Render call instead of letting FrameDiff allocate a fresh List each
    // frame — after a few frames it stabilizes at whatever the largest run count seen so far
    // was, and stops growing (List<T>'s backing array never shrinks on Clear).
    private readonly List<DiffRun> _runs = [];

    // SGR state last actually sent to the writer this frame — compared against the *emitted*
    // style, not _front's old one, so a long run of same-styled cells only pays for SetStyle
    // once. Reset at the start of every Render: carrying it across frames would assume nothing
    // else ever touches the terminal's graphic rendition state, which isn't a safe assumption
    // to bake in here.
    private Color _lastForeground;
    private Color _lastBackground;
    private StyleFlags _lastStyle;
    private bool _hasEmittedStyle;

    // Forces the next Render to treat every cell as changed — the caller's job to invoke this
    // on first frame (though a null _front already implies that) and after a terminal resize,
    // once _front's content can no longer be trusted to match the new frame.
    public void Invalidate() => _forceFullRepaint = true;

    public void Render(Buffer back, ITerminalWriter writer)
    {
        ArgumentNullException.ThrowIfNull(back);
        ArgumentNullException.ThrowIfNull(writer);

        FrameDiff.Compute(_forceFullRepaint ? null : _front, back, _runs);
        _forceFullRepaint = false;
        _hasEmittedStyle = false;

        foreach (var run in _runs)
            EmitRun(run, back, writer);

        CommitFront(back);
        writer.Flush();
    }

    private void EmitRun(DiffRun run, Buffer back, ITerminalWriter writer)
    {
        writer.MoveCursor(run.StartX, run.Row);

        for (var x = run.StartX; x <= run.EndX; x++)
        {
            var cell = back[x, run.Row];

            if (!_hasEmittedStyle ||
                !cell.Foreground.Equals(_lastForeground) ||
                !cell.Background.Equals(_lastBackground) ||
                cell.Style != _lastStyle)
            {
                writer.SetStyle(cell.Foreground, cell.Background, cell.Style);
                _lastForeground = cell.Foreground;
                _lastBackground = cell.Background;
                _lastStyle = cell.Style;
                _hasEmittedStyle = true;
            }

            writer.Write(cell.Char);
        }
    }

    private void CommitFront(Buffer back)
    {
        if (_front is null || _front.Width != back.Width || _front.Height != back.Height)
            _front = new Buffer(back.Width, back.Height);

        _front.CopyFrom(back);
    }
}
