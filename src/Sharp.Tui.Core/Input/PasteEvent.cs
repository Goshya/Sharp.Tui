namespace Sharp.Tui.Core.Input;

// The whole bracketed-paste payload as one chunk (ESC[200~ ... ESC[201~), not delivered
// character-by-character — pasting is conceptually one event, not N keystrokes.
public readonly record struct PasteEvent(string Text);
