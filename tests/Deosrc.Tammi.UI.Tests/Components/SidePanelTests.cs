using Bunit;
using Deosrc.Tammi.UI.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Deosrc.Tammi.UI.Tests.Components;

public sealed class SidePanelTests : BunitContext
{
    public SidePanelTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddFluentUIComponents();
    }

    [Fact]
    public void SidePanel_HasAFooterOnlyWhenItHasOne()
    {
        var without = Render<SidePanel>(parameters => parameters.AddChildContent("<p>Sections</p>"));
        Assert.Empty(without.FindAll(".side-panel-footer"));

        RenderFragment footer = builder => builder.AddMarkupContent(0, "<button class=\"test-footer\">Manage</button>");
        var with = Render<SidePanel>(parameters => parameters.Add(panel => panel.Footer, footer));
        Assert.Single(with.Find(".side-panel-footer").QuerySelectorAll(".test-footer"));
    }

    [Fact]
    public void Section_ShowsItsTitleContentAndActions()
    {
        RenderFragment actions = builder => builder.AddMarkupContent(0, "<button class=\"test-action\">R</button>");
        var section = Render<SidePanelSection>(parameters => parameters
            .Add(s => s.Title, "Tasks")
            .Add(s => s.Actions, actions)
            .AddChildContent("<p class=\"test-body\">Content</p>"));

        Assert.Equal("Tasks", section.Find(".side-panel-section-title").TextContent.Trim());
        Assert.Single(section.FindAll(".test-body"));
        Assert.Single(section.Find(".side-panel-section-actions").QuerySelectorAll(".test-action"));
    }

    [Fact]
    public void Section_ShowsItsHeaderContentOnlyWhileCollapsed()
    {
        RenderFragment summary = builder => builder.AddMarkupContent(0, "<span class=\"test-summary\">2 working</span>");

        var expanded = Render<SidePanelSection>(parameters => parameters
            .Add(s => s.Title, "Agents").Add(s => s.HeaderContent, summary));
        Assert.Empty(expanded.FindAll(".test-summary"));

        var collapsed = Render<SidePanelSection>(parameters => parameters
            .Add(s => s.Title, "Agents").Add(s => s.Expanded, false).Add(s => s.HeaderContent, summary));
        Assert.Single(collapsed.FindAll(".test-summary"));
    }

    [Fact]
    public void Section_HasNoActionsAreaWithoutActions()
    {
        var section = Render<SidePanelSection>(parameters => parameters.Add(s => s.Title, "Tools"));

        Assert.Empty(section.FindAll(".side-panel-section-actions"));
    }

    [Fact]
    public void Section_ShowsItsCountWhileCollapsed()
    {
        var section = Render<SidePanelSection>(parameters => parameters
            .Add(s => s.Title, "Tasks")
            .Add(s => s.Expanded, false)
            .Add(s => s.Count, 3));

        Assert.Equal("3", section.Find("fluent-counter-badge").GetAttribute("count"));
    }

    [Fact]
    public void Section_ShowsCountOfZeroWhileCollapsed()
    {
        var section = Render<SidePanelSection>(parameters => parameters
            .Add(s => s.Title, "Tasks")
            .Add(s => s.Expanded, false)
            .Add(s => s.Count, 0));

        // Zero is shown, as the badge is told to, even though it has no count attribute for it.
        Assert.NotNull(section.Find("fluent-counter-badge").GetAttribute("show-zero"));
    }

    [Fact]
    public void Section_ShowsNoCountWhileExpanded()
    {
        var section = Render<SidePanelSection>(parameters => parameters
            .Add(s => s.Title, "Tasks")
            .Add(s => s.Count, 3));

        Assert.Empty(section.FindAll("fluent-counter-badge"));
    }
}
