using Bunit;
using Deosrc.Tammi.UI.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Deosrc.Tammi.UI.Tests.Components;

public sealed class SidePanelCardTests : BunitContext
{
    public SidePanelCardTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddFluentUIComponents();
    }

    [Fact]
    public void ShowsItsHeadingBadgeAndContent()
    {
        var card = Render<SidePanelCard>(parameters => parameters
            .Add(p => p.Heading, "<span class=\"test-heading\">repo</span>")
            .Add(p => p.Badge, "Changed")
            .AddChildContent("<p class=\"test-body\">Body</p>"));

        Assert.Equal("repo", card.Find(".side-panel-card-heading .test-heading").TextContent);
        Assert.Equal("Changed", card.Find(".side-panel-card-badge").TextContent.Trim());
        Assert.Equal("Body", card.Find(".test-body").TextContent);
    }

    [Fact]
    public void HasNoBadgeWithoutBadgeText()
    {
        var card = Render<SidePanelCard>();

        Assert.Empty(card.FindAll(".side-panel-card-badge"));
    }

    [Theory]
    [InlineData(SidePanelCardTone.Neutral, "side-panel-card-neutral")]
    [InlineData(SidePanelCardTone.Info, "side-panel-card-info")]
    [InlineData(SidePanelCardTone.Success, "side-panel-card-success")]
    [InlineData(SidePanelCardTone.Warning, "side-panel-card-warning")]
    [InlineData(SidePanelCardTone.Danger, "side-panel-card-danger")]
    public void ToneIsAClassOfTheItem(SidePanelCardTone tone, string expectedClass)
    {
        var card = Render<SidePanelCard>(parameters => parameters
            .Add(p => p.Tone, tone)
            .Add(p => p.Badge, "Badge"));

        Assert.Contains(expectedClass, card.Find("li").ClassList);
    }

    [Fact]
    public void IsClickableOnlyWithAnOnClick()
    {
        Assert.DoesNotContain("side-panel-card-clickable", Render<SidePanelCard>().Find("li").ClassList);

        var clicked = false;
        var card = Render<SidePanelCard>(parameters => parameters
            .Add(p => p.OnClick, (MouseEventArgs _) => clicked = true));

        Assert.Contains("side-panel-card-clickable", card.Find("li").ClassList);
        card.Find(".fluent-card").Click();
        Assert.True(clicked);
    }
}
