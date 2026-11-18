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

    private static TabBarItem Item(string key, string? label = null, bool active = false, RenderFragment? indicator = null) =>
        new(key, "/" + key, key + " tooltip", label, Indicator: indicator, Active: active);

    private IRenderedComponent<TabBar> Render(
        IReadOnlyList<TabBarItem>? fixedItems = null,
        IReadOnlyList<TabBarItem>? items = null,
        Action<TabBarItem>? onClose = null,
        RenderFragment? end = null,
        RenderFragment? trailing = null) =>
        Render<TabBar>(parameters => parameters
            .Add(bar => bar.AriaLabel, "Main")
            .Add(bar => bar.FixedItems, fixedItems ?? [])
            .Add(bar => bar.Items, items ?? [])
            .Add(bar => bar.OnClose, EventCallback.Factory.Create<TabBarItem>(this, item => onClose?.Invoke(item)))
            .Add(bar => bar.EndContent, end)
            .Add(bar => bar.TrailingContent, trailing));

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
    public void EndAndTrailingContent_AreShownOutsideTheScrollingTabs()
    {
        RenderFragment end = builder => builder.AddMarkupContent(0, "<button class=\"test-end\">New</button>");
        RenderFragment trailing = builder => builder.AddMarkupContent(0, "<a class=\"test-trailing\">Settings</a>");
        var bar = Render(items: [Item("one", "One")], end: end, trailing: trailing);

        Assert.Empty(bar.Find(".tab-bar-scroll").QuerySelectorAll(".test-end, .test-trailing"));
        Assert.Single(bar.FindAll(".test-end"));
        Assert.Single(bar.Find(".tab-bar-trailing").QuerySelectorAll(".test-trailing"));
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
    public void TheMenuOfAllTabs_ListsTheFixedTabsToo()
    {
        var bar = Render(fixedItems: [Item("home", "Home")], items: [Item("one", "One"), Item("two")]);

        bar.InvokeAsync(() => bar.Instance.SetOverflowing(true));

        var labels = bar.FindAll("fluent-menu-item").Select(item => item.GetAttribute("label") ?? item.TextContent.Trim());
        Assert.Equal(["Home", "One", "two tooltip"], labels);
    }
}
