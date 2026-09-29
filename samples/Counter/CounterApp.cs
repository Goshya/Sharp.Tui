using Sharp.Tui.Runtime;
using Sharp.Tui.Widgets;

namespace Counter;

internal sealed class CounterApp : IApp<Model, Msg>
{
    public (Model Model, Cmd<Msg> Cmd) Init() => (new Model(0), Cmd.None<Msg>());

    public (Model Model, Cmd<Msg> Cmd) Update(Model model, Msg msg) => msg switch
    {
        Increment => (model with { Count = model.Count + 1 }, Cmd.None<Msg>()),
        Decrement => (model with { Count = model.Count - 1 }, Cmd.None<Msg>()),
        // Quitting is an effect that Update returns, like tea.Quit in Bubble Tea.
        QuitRequested => (model, Cmd.Quit<Msg>()),
        _ => (model, Cmd.None<Msg>()),
    };

    public Widget View(Model model) => new Column([Column.Auto(new Text($"Count: {model.Count}")),
                                                   Column.Fill(1, new Row([Row.Fill(1, new Text("+ / -  change")),
                                                                           Row.Fill(1, new Text("q  quit")),])),]);
}
