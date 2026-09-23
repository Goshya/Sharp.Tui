using System;
using System.Collections.Generic;
using System.Text;

namespace Sharp.Tui.Core.Rendering;

// A contiguous span of columns in one row that changed (or, on full-buffer invalidation,
// that must be repainted regardless). [StartX, EndX] is inclusive on both ends.
internal readonly record struct DiffRun(int Row, int StartX, int EndX);
