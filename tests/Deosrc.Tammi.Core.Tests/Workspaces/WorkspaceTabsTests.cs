using Deosrc.Tammi.Core.Workspaces;

namespace Deosrc.Tammi.Core.Tests.Workspaces;

public class WorkspaceTabsTests
{
    [Fact]
    public void Rename_ChangesTheTabsName_InPlace()
    {
        var tabs = new WorkspaceTabs();
        tabs.Open("a", "Alpha");
        tabs.Open("b", "Beta");
        var changes = 0;
        tabs.Changed += () => changes++;

        tabs.Rename("a", "Gamma");
        tabs.Rename("a", "Gamma");
        tabs.Rename("missing", "Delta");

        Assert.Equal([new WorkspaceTab("a", "Gamma"), new WorkspaceTab("b", "Beta")], tabs.Tabs);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void Open_AddsTabsInOrder()
    {
        var tabs = new WorkspaceTabs();

        tabs.Open("a", "Alpha");
        tabs.Open("b", "Beta");

        Assert.Equal(
            [new WorkspaceTab("a", "Alpha"), new WorkspaceTab("b", "Beta")],
            tabs.Tabs);
    }

    [Fact]
    public void Open_WhenAlreadyOpen_ReturnsExistingTabWithoutAddingAnother()
    {
        var tabs = new WorkspaceTabs();
        var first = tabs.Open("a", "Alpha");
        var changes = 0;
        tabs.Changed += () => changes++;

        var second = tabs.Open("a", "Alpha");

        Assert.Same(first, second);
        Assert.Single(tabs.Tabs);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void Open_RaisesChanged()
    {
        var tabs = new WorkspaceTabs();
        var changes = 0;
        tabs.Changed += () => changes++;

        tabs.Open("a", "Alpha");

        Assert.Equal(1, changes);
    }

    [Fact]
    public void Find_ReturnsOpenTabOrNull()
    {
        var tabs = new WorkspaceTabs();
        var tab = tabs.Open("a", "Alpha");

        Assert.Same(tab, tabs.Find("a"));
        Assert.Null(tabs.Find("b"));
    }

    [Fact]
    public void Close_RemovesTabAndRaisesChanged()
    {
        var tabs = new WorkspaceTabs();
        tabs.Open("a", "Alpha");
        tabs.Open("b", "Beta");
        var changes = 0;
        tabs.Changed += () => changes++;

        tabs.Close("a");

        Assert.Equal([new WorkspaceTab("b", "Beta")], tabs.Tabs);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void Close_ReturnsNextTab()
    {
        var tabs = new WorkspaceTabs();
        tabs.Open("a", "Alpha");
        tabs.Open("b", "Beta");
        tabs.Open("c", "Gamma");

        var next = tabs.Close("b");

        Assert.Equal("c", next?.WorkspaceId);
    }

    [Fact]
    public void Close_LastTab_ReturnsPreviousTab()
    {
        var tabs = new WorkspaceTabs();
        tabs.Open("a", "Alpha");
        tabs.Open("b", "Beta");

        var next = tabs.Close("b");

        Assert.Equal("a", next?.WorkspaceId);
    }

    [Fact]
    public void Close_OnlyTab_ReturnsNull()
    {
        var tabs = new WorkspaceTabs();
        tabs.Open("a", "Alpha");

        Assert.Null(tabs.Close("a"));
        Assert.Empty(tabs.Tabs);
    }

    [Fact]
    public void Close_WhenNotOpen_DoesNothing()
    {
        var tabs = new WorkspaceTabs();
        tabs.Open("a", "Alpha");
        var changes = 0;
        tabs.Changed += () => changes++;

        Assert.Null(tabs.Close("b"));
        Assert.Single(tabs.Tabs);
        Assert.Equal(0, changes);
    }
}
