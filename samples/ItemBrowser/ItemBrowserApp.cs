using Sharp.Tui.Runtime;
using Sharp.Tui.Widgets;

namespace ItemBrowser;

// M4.7 (#23): proves Block/ListView/Row/Column/Style/Theme all work together in a real terminal
// — none of this has ever been drawn outside a Buffer/golden-file test before. The item list is
// kept short enough to always fit without scrolling (ScrollOffset is always 0): the point here is
// composing widgets and seeing real colors/borders, not building scroll-aware Update logic.
internal sealed class ItemBrowserApp : IApp<Model, Msg>
{
    private static readonly (string Name, string Description)[] Items =
    [
        ("Apple", "A crisp, sweet fruit that keeps the doctor away."),
        ("Banana", "A curved, potassium-rich fruit with a peelable skin."),
        ("Cherry", "A small, deep-red stone fruit, often sold in pairs."),
        ("Date", "A sticky-sweet fruit that grows on palm trees."),
        ("Elderberry", "A dark purple berry used in syrups and wines."),
    ];

    public (Model Model, Cmd<Msg> Cmd) Init() => (new Model(Items, SelectedIndex: 0), Cmd.None<Msg>());

    public (Model Model, Cmd<Msg> Cmd) Update(Model model, Msg msg) => msg switch
    {
        MoveUp => (model with { SelectedIndex = Math.Max(0, model.SelectedIndex - 1) }, Cmd.None<Msg>()),
        MoveDown => (model with { SelectedIndex = Math.Min(model.Items.Count - 1, model.SelectedIndex + 1) }, Cmd.None<Msg>()),
        QuitRequested => (model, Cmd.Quit<Msg>()),
        _ => (model, Cmd.None<Msg>()),
    };

    public Widget View(Model model)
    {
        var itemNames = model.Items.Select(item => item.Name).ToArray();
        var (name, description) = model.Items[model.SelectedIndex];

        return new Row([
            Row.Fixed(20, new Block(
                new ListView(itemNames, model.SelectedIndex, scrollOffset: 0, selectedItemStyle: Theme.Default.Selected),
                title: "Items",
                borderStyle: Theme.Default.Border,
                titleStyle: Theme.Default.Title)),
            // A 1-column gap — otherwise the two Blocks' borders touch directly (no padding/gap
            // feature exists yet on any container, so this is the only way to get one).
            Row.Fixed(1, new Text("")),
            Row.Fill(1, new Block(
                new Text($"{name}\n\n{description}"),
                title: "Details",
                borderStyle: Theme.Default.Border,
                titleStyle: Theme.Default.Title)),
        ]);
    }
}
