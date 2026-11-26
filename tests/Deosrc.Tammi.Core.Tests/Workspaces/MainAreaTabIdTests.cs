using Deosrc.Tammi.Core.Workspaces;

namespace Deosrc.Tammi.Core.Tests.Workspaces;

public sealed class MainAreaTabIdTests
{
    [Fact]
    public void Id_IsDifferentForEveryTab()
    {
        var tabs = new WorkspaceHistoryTabs();

        IMainAreaTab first = tabs.Open("w1");
        IMainAreaTab second = tabs.Open("w2");

        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void Id_StaysTheSameForATab()
    {
        IMainAreaTab tab = new WorkspaceHistoryTabs().Open("w1");

        Assert.Equal(tab.Id, tab.Id);
    }

    [Fact]
    public void Id_NeedsNoEscapingInAnAddress()
    {
        IMainAreaTab tab = new WorkspaceHistoryTabs().Open("w1");

        Assert.Equal(Uri.EscapeDataString(tab.Id), tab.Id);
    }
}
