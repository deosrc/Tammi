using Deosrc.Tammi.UI.Components;

namespace Deosrc.Tammi.UI.Tests.Components;

public sealed class CardExpansionTests
{
    private static CardExpansion With(params int[] expanded)
    {
        var expansion = new CardExpansion();
        foreach (var index in expanded)
        {
            expansion.Expand(index);
        }

        return expansion;
    }

    private static int[] Expanded(CardExpansion expansion) =>
        [.. Enumerable.Range(0, 10).Where(expansion.IsExpanded)];

    [Fact]
    public void Cards_StartCollapsed() => Assert.Empty(Expanded(new CardExpansion()));

    [Fact]
    public void Toggle_ExpandsACollapsedCardAndCollapsesAnExpandedOne()
    {
        var expansion = new CardExpansion();

        expansion.Toggle(2);
        Assert.Equal([2], Expanded(expansion));

        expansion.Toggle(2);
        Assert.Empty(Expanded(expansion));
    }

    [Fact]
    public void Collapse_CollapsesAnExpandedCardAndLeavesAnotherAlone()
    {
        var expansion = With(1, 2);

        expansion.Collapse(1);
        expansion.Collapse(5);

        Assert.Equal([2], Expanded(expansion));
    }

    [Fact]
    public void Removed_MovesTheCardsAfterItUpAndForgetsTheRemovedOne()
    {
        var expansion = With(1, 2, 4);

        expansion.Removed(2);

        Assert.Equal([1, 3], Expanded(expansion));
    }

    [Fact]
    public void Removed_LeavesTheCardsBeforeItAlone()
    {
        var expansion = With(0, 1);

        expansion.Removed(3);

        Assert.Equal([0, 1], Expanded(expansion));
    }

    [Fact]
    public void Moved_DownCarriesTheCardsStateAndShiftsTheOnesItPassed()
    {
        var expansion = With(0, 2);

        expansion.Moved(from: 0, to: 2);

        // The card at 0 is now at 2, and the one that was at 2 is now at 1.
        Assert.Equal([1, 2], Expanded(expansion));
    }

    [Fact]
    public void Moved_UpCarriesTheCardsStateAndShiftsTheOnesItPassed()
    {
        var expansion = With(1, 3);

        expansion.Moved(from: 3, to: 1);

        // The card at 3 is now at 1, and the one that was at 1 is now at 2.
        Assert.Equal([1, 2], Expanded(expansion));
    }

    [Fact]
    public void Moved_LeavesTheCardsOutsideTheMoveAlone()
    {
        var expansion = With(0, 5);

        expansion.Moved(from: 1, to: 3);

        Assert.Equal([0, 5], Expanded(expansion));
    }
}
