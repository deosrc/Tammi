using Deosrc.Tammi.Core.Workspaces;

namespace Deosrc.Tammi.Core.Tests.Workspaces;

public sealed class MainAreaTabArrangementTests
{
    private readonly WorkspaceHistoryTabs _tabs = new();

    [Fact]
    public void Arrange_WithoutMoves_IsInTheOrderTheTabsOpened()
    {
        IMainAreaTab first = _tabs.Open("w1");
        IMainAreaTab second = _tabs.Open("w2");

        Assert.Equal([first, second], new MainAreaTabArrangement().Arrange("w", [second, first]));
    }

    [Fact]
    public void Move_PutsTheTabAtItsNewPlace_AndNewTabsComeAfter()
    {
        var arrangement = new MainAreaTabArrangement();
        IMainAreaTab a = _tabs.Open("a");
        IMainAreaTab b = _tabs.Open("b");
        IMainAreaTab c = _tabs.Open("c");

        arrangement.Move("w", [a, b, c], c.Id, 0);
        IMainAreaTab d = _tabs.Open("d");

        Assert.Equal([c, a, b, d], arrangement.Arrange("w", [a, b, c, d]));
    }

    [Fact]
    public void Move_IsKeptForEachWorkspace()
    {
        var arrangement = new MainAreaTabArrangement();
        IMainAreaTab a = _tabs.Open("a");
        IMainAreaTab b = _tabs.Open("b");

        arrangement.Move("w1", [a, b], b.Id, 0);

        Assert.Equal([a, b], arrangement.Arrange("w2", [a, b]));
    }

    [Fact]
    public void Move_RaisesChangedWithTheWorkspace_OnlyWhenTheOrderChanges()
    {
        var arrangement = new MainAreaTabArrangement();
        IMainAreaTab a = _tabs.Open("a");
        IMainAreaTab b = _tabs.Open("b");
        var changed = new List<string>();
        arrangement.Changed += changed.Add;

        arrangement.Move("w", [a, b], b.Id, 0);
        arrangement.Move("w", [b, a], b.Id, 0);
        arrangement.Move("w", [b, a], "not-a-tab", 0);

        Assert.Equal(["w"], changed);
    }
}
