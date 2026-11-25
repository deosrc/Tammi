using AngleSharp.Dom;
using Bunit;
using Deosrc.Tammi.UI.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Deosrc.Tammi.UI.Tests.Components;

public sealed class TabBarTests : BunitContext
{
    public TabBarTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddFluentUIComponents();
    }

    private static TabBarItem Item(
        string key, string? label = null, bool active = false, RenderFragment? indicator = null, bool pinned = false) =>
        new(key, "/" + key, key + " tooltip", label, Indicator: indicator, Active: active, PinnedLast: pinned);

    private IRenderedComponent<TabBar> Render(
        IReadOnlyList<TabBarItem>? fixedItems = null,
        IReadOnlyList<TabBarItem>? items = null,
        Action<TabBarItem>? onClose = null,
        RenderFragment? end = null,
        IReadOnlyList<TabBarItem>? trailing = null,
        Action<TabBarMove>? onMove = null) =>
        Render<TabBar>(parameters => parameters
            .Add(bar => bar.OnMove, onMove is null ? default : EventCallback.Factory.Create(this, onMove))
            .Add(bar => bar.AriaLabel, "Main")
            .Add(bar => bar.FixedItems, fixedItems ?? [])
            .Add(bar => bar.Items, items ?? [])
            .Add(bar => bar.OnClose, EventCallback.Factory.Create<TabBarItem>(this, item => onClose?.Invoke(item)))
            .Add(bar => bar.EndContent, end)
            .Add(bar => bar.TrailingItems, trailing ?? []));

    [Fact]
    public void Tabs_AreLinksToTheirAddresses()
    {
        var bar = Render(fixedItems: [Item("home", "Home")], items: [Item("one", "One"), Item("two", "Two")]);

        var links = bar.FindAll("a.tab-bar-label");

        Assert.Equal(["/home", "/one", "/two"], links.Select(link => link.GetAttribute("href")));
        Assert.Equal(["Home", "One", "Two"], links.Select(link => link.TextContent.Trim()));
    }

    [Fact]
    public void ActiveTab_IsMarkedAsTheCurrentPage()
    {
        var bar = Render(items: [Item("one", "One"), Item("two", "Two", active: true)]);

        var links = bar.FindAll("a.tab-bar-label");

        Assert.Null(links[0].GetAttribute("aria-current"));
        Assert.Equal("page", links[1].GetAttribute("aria-current"));
        Assert.Contains("active", bar.FindAll(".tab-bar-tab")[1].ClassList);
    }

    [Fact]
    public void TabWithoutALabel_ShowsOnlyItsIconAndNamesItByItsTitle()
    {
        var bar = Render(fixedItems: [Item("home")]);

        var link = bar.Find("a.tab-bar-label");

        Assert.Equal("", link.TextContent.Trim());
        Assert.Equal("home tooltip", link.GetAttribute("aria-label"));
        Assert.Contains("icon-only", link.ClassList);
    }

    [Fact]
    public void OnlyTheClosableTabsHaveACloseButton()
    {
        var bar = Render(fixedItems: [Item("home", "Home")], items: [Item("one", "One"), Item("two", "Two")]);

        Assert.Empty(bar.FindAll(".tab-bar-tab:not(.closable) .tab-bar-close"));
        Assert.Equal(2, bar.FindAll(".tab-bar-tab.closable .tab-bar-close").Count);
    }

    [Fact]
    public void ClosingATab_RaisesOnCloseWithThatTab()
    {
        var one = Item("one", "One");
        var two = Item("two", "Two");
        TabBarItem? closed = null;
        var bar = Render(items: [one, two], onClose: item => closed = item);

        bar.FindAll(".tab-bar-close")[1].Click();

        Assert.Equal(two, closed);
    }

    [Fact]
    public void Indicator_IsShownAfterTheLabelOfItsTabOnly()
    {
        RenderFragment dot = builder =>
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "class", "test-dot");
            builder.CloseElement();
        };
        var bar = Render(items: [Item("one", "One", indicator: dot), Item("two", "Two")]);

        var tabs = bar.FindAll(".tab-bar-tab");

        Assert.Single(tabs[0].QuerySelectorAll(".test-dot"));
        Assert.Empty(tabs[1].QuerySelectorAll(".test-dot"));
    }

    [Fact]
    public void EndContent_IsShownAfterTheScrollingTabsAndOutsideThem()
    {
        RenderFragment end = builder => builder.AddMarkupContent(0, "<button class=\"test-end\">New</button>");
        var bar = Render(items: [Item("one", "One")], end: end);

        Assert.Empty(bar.Find(".tab-bar-scroll").QuerySelectorAll(".test-end"));
        Assert.Single(bar.FindAll(".test-end"));
    }

    [Fact]
    public void TrailingTabs_AreAtTheFarEndAndCantBeClosed()
    {
        var bar = Render(items: [Item("one", "One")], trailing: [Item("settings", "Settings")]);

        var trailing = bar.Find(".tab-bar-trailing");

        Assert.Equal("/settings", trailing.QuerySelector("a.tab-bar-label")!.GetAttribute("href"));
        Assert.Empty(trailing.QuerySelectorAll(".tab-bar-close"));
    }

    [Fact]
    public void TheMenuOfAllTabs_IsOnlyOfferedWhileTheTabsOverflow()
    {
        var bar = Render(fixedItems: [Item("home", "Home")], items: [Item("one", "One")]);
        Assert.Empty(bar.FindAll(".tab-bar-overflow"));

        bar.InvokeAsync(() => bar.Instance.SetOverflowing(true));
        Assert.Single(bar.FindAll(".tab-bar-overflow"));

        bar.InvokeAsync(() => bar.Instance.SetOverflowing(false));
        Assert.Empty(bar.FindAll(".tab-bar-overflow"));
    }

    [Fact]
    public void TheMenuOfTabs_ListsOnlyTheTabsThatScroll()
    {
        var bar = Render(
            fixedItems: [Item("home", "Home")],
            items: [Item("one", "One"), Item("two")],
            trailing: [Item("settings", "Settings")]);

        bar.InvokeAsync(() => bar.Instance.SetOverflowing(true));

        var labels = bar.FindAll(".tab-bar-menu-item").Select(item => item.TextContent.Trim());
        Assert.Equal(["One", "two tooltip"], labels);
    }

    [Fact]
    public void TheMenuOfTabs_ShowsEachTabsIcon()
    {
        RenderFragment icon = builder => builder.AddMarkupContent(0, "<span class=\"test-icon\"></span>");
        var withIcon = Item("one", "One") with { Icon = icon };
        var bar = Render(items: [withIcon, Item("two", "Two")]);

        bar.InvokeAsync(() => bar.Instance.SetOverflowing(true));

        var menuItems = bar.FindAll(".tab-bar-menu-item");
        Assert.Single(menuItems[0].QuerySelectorAll(".test-icon"));
        Assert.Empty(menuItems[1].QuerySelectorAll(".test-icon"));
    }

    [Fact]
    public void OnlyTheClosableTabsThatArentPinned_CanBeMoved()
    {
        var bar = Render(
            fixedItems: [Item("home", "Home")],
            items: [Item("one", "One"), Item("usage", "Usage", pinned: true)],
            trailing: [Item("settings", "Settings")],
            onMove: _ => { });

        Assert.Equal(["one"], bar.FindAll(".tab-bar-tab.movable").Select(tab => tab.GetAttribute("data-key")));
    }

    [Fact]
    public void WithoutOnMove_NoTabCanBeMoved()
    {
        var bar = Render(items: [Item("one", "One"), Item("two", "Two")]);

        Assert.Empty(bar.FindAll(".tab-bar-tab.movable"));
    }

    [Theory]
    [InlineData("one", "three", true, 2)]
    [InlineData("three", "one", false, 0)]
    [InlineData("one", "two", true, 1)]
    // Never after the pinned tab.
    [InlineData("one", "usage", true, 2)]
    public async Task MovingATabNextToAnother_RaisesOnMoveWithItsNewPlace(string key, string other, bool after, int index)
    {
        TabBarMove? moved = null;
        var bar = Render(
            items: [Item("one", "One"), Item("two", "Two"), Item("three", "Three"), Item("usage", "Usage", pinned: true)],
            onMove: move => moved = move);

        await bar.InvokeAsync(() => bar.Instance.MoveTab(key, other, after));

        Assert.Equal(key, moved?.Item.Key);
        Assert.Equal(index, moved?.Index);
    }

    [Theory]
    // Already there.
    [InlineData("two", "one", true)]
    [InlineData("usage", "one", false)]
    [InlineData("one", "missing", true)]
    public async Task MovingATabWhereItCantGo_RaisesNothing(string key, string other, bool after)
    {
        TabBarMove? moved = null;
        var bar = Render(
            items: [Item("one", "One"), Item("two", "Two"), Item("usage", "Usage", pinned: true)],
            onMove: move => moved = move);

        await bar.InvokeAsync(() => bar.Instance.MoveTab(key, other, after));

        Assert.Null(moved);
    }
}
