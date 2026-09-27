namespace Sharp.Tui.Layout;

// A measured size — Widget.Measure's result once Measure exists (M3.4). Defined now so
// Constraints, which a measured Size is clamped into, has something concrete to refer to.
public readonly record struct Size(int Width, int Height);
