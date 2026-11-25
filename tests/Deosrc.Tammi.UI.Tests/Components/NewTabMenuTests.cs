using Bunit;
using Deosrc.Tammi.Core.Agents;
using Deosrc.Tammi.Core.Workspaces;
using Deosrc.Tammi.UI.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Deosrc.Tammi.UI.Tests.Components;

public sealed class NewTabMenuTests : BunitContext
{
    private static readonly WorkspaceRepository Api = new("api", "/repos/api", "acme", "origin/main");
    private static readonly AgentDefinition Agent = new("Helper", "helper");

    public NewTabMenuTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddFluentUIComponents();
    }

    [Fact]
    public void WithoutRepositories_TerminalAndAgentsHaveNoSubmenu()
    {
        var menu = Render<NewTabMenu>(parameters => parameters.Add(p => p.AgentDefinitions, [Agent]));

        var items = menu.FindAll("fluent-menu-item");
        Assert.Equal(2, items.Count);
        Assert.Contains("Terminal", items[0].TextContent);
        Assert.Contains("Helper", items[1].TextContent);
        Assert.DoesNotContain("Workspace", menu.Markup);
    }

    [Fact]
    public void WithRepositories_TerminalAndAgentsOfferWhereToStart()
    {
        var menu = Render<NewTabMenu>(parameters => parameters
            .Add(p => p.AgentDefinitions, [Agent])
            .Add(p => p.Repositories, [Api]));

        var items = menu.FindAll("fluent-menu-item");
        Assert.Equal(2, items.Count(item => item.TextContent.Trim() == "Workspace"));
        Assert.Equal(2, items.Count(item => item.TextContent.Trim() == "Repository: api"));
    }

    [Fact]
    public void HasAGitItemOnlyWithGitTargets()
    {
        Assert.DoesNotContain("Git", Render<NewTabMenu>(p => p.Add(m => m.Repositories, [Api])).Markup);

        var menu = Render<NewTabMenu>(parameters => parameters
            .Add(p => p.Repositories, [Api])
            .Add(p => p.GitTargets, [(Api, (string?)null), (Api, "libs/core")]));

        Assert.Contains("Git", menu.Markup);
        Assert.Contains("Submodule: api/libs/core", menu.Markup);
    }
}
