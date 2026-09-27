using Sharp.Tui.Layout;

namespace Sharp.Tui.Tests.Layout;

public class LayoutSolverTests
{
    [Fact]
    public void Solve_NullModes_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => LayoutSolver.Solve(10, null!));
    }

    [Fact]
    public void Solve_NegativeAvailable_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LayoutSolver.Solve(-1, []));
    }

    [Fact]
    public void Solve_EmptyModes_ReturnsEmptyArray()
    {
        Assert.Empty(LayoutSolver.Solve(10, []));
    }

    [Fact]
    public void Solve_AvailableZero_EveryEntryGetsZero()
    {
        var sizes = LayoutSolver.Solve(0, [SizeMode.Fixed(5), SizeMode.Percent(50), SizeMode.Fill(1)]);

        Assert.Equal([0, 0, 0], sizes);
    }

    // --- Fixed ---------------------------------------------------------------------------------

    [Fact]
    public void Solve_PureFixed_ReturnsEachOwnSize()
    {
        var sizes = LayoutSolver.Solve(30, [SizeMode.Fixed(10), SizeMode.Fixed(20)]);

        Assert.Equal([10, 20], sizes);
    }

    [Fact]
    public void Solve_FixedSumExceedingAvailable_LaterEntriesGetZero()
    {
        var sizes = LayoutSolver.Solve(10, [SizeMode.Fixed(6), SizeMode.Fixed(6), SizeMode.Fixed(6)]);

        Assert.Equal([6, 4, 0], sizes);
    }

    // --- Fill ------------------------------------------------------------------------------------

    [Fact]
    public void Solve_PureFill_EqualWeights_SplitsEvenly()
    {
        var sizes = LayoutSolver.Solve(10, [SizeMode.Fill(1), SizeMode.Fill(1)]);

        Assert.Equal([5, 5], sizes);
    }

    [Fact]
    public void Solve_PureFill_UnequalWeights_SplitsProportionally()
    {
        var sizes = LayoutSolver.Solve(30, [SizeMode.Fill(1), SizeMode.Fill(2)]);

        Assert.Equal([10, 20], sizes);
    }

    [Fact]
    public void Solve_PureFill_UnevenSplit_UsesLargestRemainderAndSumsExactly()
    {
        // 10 / 3 = 3.33 each: base 3+3+3=9, one cell left over goes to the first entry (ties
        // keep list order, since all three have the same fractional remainder).
        var sizes = LayoutSolver.Solve(10, [SizeMode.Fill(1), SizeMode.Fill(1), SizeMode.Fill(1)]);

        Assert.Equal([4, 3, 3], sizes);
        Assert.Equal(10, sizes.Sum());
    }

    [Fact]
    public void Solve_FillWithZeroWeight_GetsZero()
    {
        var sizes = LayoutSolver.Solve(10, [SizeMode.Fill(0), SizeMode.Fill(1)]);

        Assert.Equal([0, 10], sizes);
    }

    [Fact]
    public void Solve_AllFillWeightsZero_EveryEntryGetsZero()
    {
        var sizes = LayoutSolver.Solve(10, [SizeMode.Fill(0), SizeMode.Fill(0)]);

        Assert.Equal([0, 0], sizes);
    }

    // --- Mixed Fixed + Fill ----------------------------------------------------------------------

    [Fact]
    public void Solve_FixedThenFill_FillGetsWhatIsLeft()
    {
        var sizes = LayoutSolver.Solve(30, [SizeMode.Fixed(10), SizeMode.Fill(1)]);

        Assert.Equal([10, 20], sizes);
    }

    [Fact]
    public void Solve_FixedExhaustsBudget_FillGetsZero()
    {
        var sizes = LayoutSolver.Solve(10, [SizeMode.Fixed(15), SizeMode.Fill(1)]);

        Assert.Equal([10, 0], sizes);
    }

    // --- Percent -----------------------------------------------------------------------------------

    [Fact]
    public void Solve_Percent_IsOfAvailableNotOfRemainderAfterFixed()
    {
        // Percent(50) of 100 is 50, regardless of the Fixed(10) sibling eating into the budget.
        var sizes = LayoutSolver.Solve(100, [SizeMode.Fixed(10), SizeMode.Percent(50)]);

        Assert.Equal([10, 50], sizes);
    }

    [Fact]
    public void Solve_FixedPlusPercentExceedingAvailable_LaterEntryGetsZero()
    {
        var sizes = LayoutSolver.Solve(10, [SizeMode.Fixed(8), SizeMode.Percent(50)]);

        Assert.Equal([8, 2], sizes);
    }

    [Fact]
    public void Solve_PercentThenFill_FillGetsWhatIsLeft()
    {
        var sizes = LayoutSolver.Solve(100, [SizeMode.Percent(30), SizeMode.Fill(1)]);

        Assert.Equal([30, 70], sizes);
    }

    [Fact]
    public void Solve_MixOfFixedPercentAndFill_AllThreeCombine()
    {
        var sizes = LayoutSolver.Solve(100, [SizeMode.Fixed(10), SizeMode.Percent(20), SizeMode.Fill(1), SizeMode.Fill(1)]);

        Assert.Equal([10, 20, 35, 35], sizes);
    }

    // --- Invariant -----------------------------------------------------------------------------

    [Theory]
    [InlineData(10, new[] { 1 })]
    [InlineData(10, new[] { 1, 1, 1 })]
    [InlineData(7, new[] { 1, 2, 3 })]
    [InlineData(1, new[] { 1, 1, 1, 1, 1 })]
    public void Solve_WithAtLeastOneFill_AlwaysSumsToExactlyAvailable(int available, int[] weights)
    {
        var modes = weights.Select(SizeMode.Fill).ToArray();

        var sizes = LayoutSolver.Solve(available, modes);

        Assert.Equal(available, sizes.Sum());
    }

    [Fact]
    public void Solve_FillAfterFixedOverflow_StillSumsToAvailable()
    {
        // Fixed alone already exceeds `available` — Fill's share of what's left is 0, but the
        // total (Fixed's clamped size + Fill's 0) still accounts for every cell of `available`.
        var sizes = LayoutSolver.Solve(5, [SizeMode.Fixed(10), SizeMode.Fill(1)]);

        Assert.Equal(5, sizes.Sum());
    }

    // --- Nested tree from #3's "Done when" ------------------------------------------------------

    [Fact]
    public void Solve_RowOfColumnAndFixed_MatchesTheIssueExample()
    {
        // Row { Column { Fixed(10), Fill(1) }, Fixed(20) } in a Row 50 cells wide: the Column
        // gets whatever's left after the sibling Fixed(20), and inside it the same solver runs
        // again along the vertical axis.
        var rowSizes = LayoutSolver.Solve(50, [SizeMode.Fill(1), SizeMode.Fixed(20)]);
        Assert.Equal([30, 20], rowSizes);

        var columnSizes = LayoutSolver.Solve(24, [SizeMode.Fixed(10), SizeMode.Fill(1)]);
        Assert.Equal([10, 14], columnSizes);
    }

    // --- Auto ------------------------------------------------------------------------------------

    [Fact]
    public void Solve_TwoArgumentOverload_AutoEntry_ThrowsWhenActuallyMeasured()
    {
        // The 2-argument overload is for callers who never use Auto — if one slips through
        // anyway, it must fail loudly, not silently NullReferenceException on a missing delegate.
        Assert.Throws<InvalidOperationException>(() => LayoutSolver.Solve(10, [SizeMode.Auto()]));
    }

    [Fact]
    public void Solve_ThreeArgumentOverload_NullMeasureAuto_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => LayoutSolver.Solve(10, [], null!));
    }

    [Fact]
    public void Solve_Auto_UsesMeasureAutoResult()
    {
        var sizes = LayoutSolver.Solve(10, [SizeMode.Auto()], _ => 4);

        Assert.Equal([4], sizes);
    }

    [Fact]
    public void Solve_Auto_OnlyCalledForAutoEntries()
    {
        var calledFor = new List<int>();
        int MeasureAuto(int i)
        {
            calledFor.Add(i);
            return 3;
        }

        LayoutSolver.Solve(10, [SizeMode.Fixed(1), SizeMode.Auto(), SizeMode.Fill(1)], MeasureAuto);

        Assert.Equal([1], calledFor);
    }

    [Fact]
    public void Solve_AutoMixedWithFixedAndFill_FillGetsWhatIsLeft()
    {
        var sizes = LayoutSolver.Solve(30, [SizeMode.Fixed(5), SizeMode.Auto(), SizeMode.Fill(1)], _ => 10);

        Assert.Equal([5, 10, 15], sizes);
    }

    [Fact]
    public void Solve_AutoExceedingAvailable_IsClampedNotOverflowing()
    {
        var sizes = LayoutSolver.Solve(10, [SizeMode.Auto(), SizeMode.Fill(1)], _ => 999);

        Assert.Equal([10, 0], sizes);
        Assert.Equal(10, sizes.Sum());
    }

    [Fact]
    public void Solve_FixedThenAuto_FirstComeFirstServedOutOfSharedBudget()
    {
        // Fixed(8) leaves only 2 for the measured Auto entry, even though it "wants" 5.
        var sizes = LayoutSolver.Solve(10, [SizeMode.Fixed(8), SizeMode.Auto()], _ => 5);

        Assert.Equal([8, 2], sizes);
    }
}
