using Sharp.Tui.Runtime;
using Sharp.Tui.Widgets;

namespace Stopwatch;

internal sealed class StopwatchApp : IApp<Model, Msg>
{
    public (Model Model, Cmd<Msg> Cmd) Init() => (new Model(TimeSpan.Zero, false, null), Cmd.None<Msg>());

    public (Model Model, Cmd<Msg> Cmd) Update(Model model, Msg msg) => msg switch
    {
        Tick tick => (model with
        {
            Elapsed = model.Running && model.LastTick is { } last ? model.Elapsed + (tick.Now - last) : model.Elapsed,
            LastTick = tick.Now,
        }, Cmd.None<Msg>()),
        ToggleRunning => (model with { Running = !model.Running }, Cmd.None<Msg>()),
        Reset => (model with { Elapsed = TimeSpan.Zero }, Cmd.None<Msg>()),
        QuitRequested => (model, Cmd.Quit<Msg>()),
        _ => (model, Cmd.None<Msg>()),
    };

    public Widget View(Model model) =>
        new Text($"{model.Elapsed:mm\\:ss\\.f}  {(model.Running ? "running" : "stopped")}\n\nspace  start/stop    r  reset    q  quit");
}
