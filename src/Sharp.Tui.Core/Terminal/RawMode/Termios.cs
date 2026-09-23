using System.Runtime.InteropServices;

namespace Sharp.Tui.Core.Terminal;

// Mirrors Linux (glibc) struct termios from <bits/termios.h> field-for-field — layout matters
// here, this is marshaled directly against the kernel via tcgetattr/tcsetattr. Do NOT reuse
// this for macOS/BSD: their struct termios has no c_line field and a c_cc of 20 bytes, not 32.
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct Termios
{
    public uint c_iflag;
    public uint c_oflag;
    public uint c_cflag;
    public uint c_lflag;
    public byte c_line;
    public fixed byte c_cc[32]; // NCCS on Linux
    public uint c_ispeed;
    public uint c_ospeed;
}
