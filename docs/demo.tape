# VHS script for the README hero GIF (docs/SPEC.md §9).
# Run from the repository root: `vhs docs/demo.tape` (requires https://github.com/charmbracelet/vhs).
# Produces docs/demo.gif — re-run any time GitLogViewer's look changes.

Output docs/demo.gif

Require dotnet
Require git

Set Shell bash
Set FontSize 16
Set Width 1000
Set Height 600
Set Theme "Dracula"
Set TypingSpeed 60ms

Type "dotnet run --project samples/GitLogViewer"
Enter
# Give the Cmd time to actually load this repository's commit history.
Sleep 2s

Down@300ms 6
Sleep 1s
Down@300ms 6
Sleep 1s
Up@300ms 4
Sleep 1.5s

Type "q"
Sleep 500ms
