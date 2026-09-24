using Sharp.Tui.Widgets;

namespace Sharp.Tui.Runtime;

public interface IApp<TModel, TMsg>
{
    (TModel Model, Cmd<TMsg> Cmd) Init();
    (TModel Model, Cmd<TMsg> Cmd) Update(TModel model, TMsg msg);
    Widget View(TModel model);
}
