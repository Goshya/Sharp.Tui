using System;
using System.Collections.Generic;
using System.Text;

namespace Sharp.Tui.Core.Rendering;

public enum ColorKind : byte
{
    Default,
    Named16,
    Indexed256,
    Rgb,
}
