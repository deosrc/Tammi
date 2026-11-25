using Bunit;
using Deosrc.Tammi.UI.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Deosrc.Tammi.UI.Tests.Components;

public sealed class PointMenuTests : BunitContext
{
    public PointMenuTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddFluentUIComponents();
    }

    [Fact]
    public void IsAnchoredAtThePointAndShowsItsItems()
    {
        var menu = Render<PointMenu>(parameters => parameters
            .Add(p => p.X, 120.5)
            .Add(p => p.Y, 48)
            .Add(p => p.Label, "Tools")
            .AddChildContent("<fluent-menu-item class=\"test-item\">Open</fluent-menu-item>"));

        var anchor = menu.Find(".point-menu-anchor");
        Assert.Contains("left: 120.5px", anchor.GetAttribute("style"));
        Assert.Contains("top: 48px", anchor.GetAttribute("style"));
        Assert.Equal(anchor.Id, menu.Find("fluent-menu").GetAttribute("trigger") ?? anchor.Id);
        Assert.Single(menu.FindAll(".test-item"));
    }

    [Fact]
    public void OpensItsMenuOnceRendered()
    {
        var module = JSInterop.SetupModule("./_content/Deosrc.Tammi.UI/Components/PointMenu.razor.js");
        module.SetupVoid("open", _ => true);

        Render<PointMenu>(parameters => parameters.Add(p => p.Label, "Tools"));

        Assert.Single(module.Invocations["open"]);
    }
}
