using Bunit;
using Deosrc.Tammi.UI.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Deosrc.Tammi.UI.Tests.Components;

public sealed class SidePanelListItemTests : BunitContext
{
    public SidePanelListItemTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddFluentUIComponents();
    }

    [Fact]
    public void ShowsItsIconAndName()
    {
        var item = Render<SidePanelListItem>(parameters => parameters
            .Add(p => p.Icon, "<i class=\"test-icon\"></i>")
            .AddChildContent("VS Code"));

        Assert.NotNull(item.Find("li .side-panel-list-icon .test-icon"));
        Assert.Equal("VS Code", item.Find(".side-panel-list-text").TextContent);
    }

    [Fact]
    public void ClickingRaisesOnClick()
    {
        var clicked = false;
        var item = Render<SidePanelListItem>(parameters => parameters
            .Add(p => p.OnClick, (MouseEventArgs _) => clicked = true));

        item.Find("fluent-button").Click();

        Assert.True(clicked);
    }

    [Fact]
    public void PulsesWithAlternatingAnimationsSoItPlaysAgain()
    {
        Assert.DoesNotContain("side-panel-list-pulse", Render<SidePanelListItem>().Find("fluent-button").ClassName);

        var even = Render<SidePanelListItem>(parameters => parameters.Add(p => p.Pulse, 2));
        var odd = Render<SidePanelListItem>(parameters => parameters.Add(p => p.Pulse, 3));

        Assert.Contains("side-panel-list-pulse", even.Find("fluent-button").ClassList);
        Assert.DoesNotContain("side-panel-list-pulse-again", even.Find("fluent-button").ClassList);
        Assert.Contains("side-panel-list-pulse-again", odd.Find("fluent-button").ClassList);
    }
}
