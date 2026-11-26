using Bunit;
using Deosrc.Tammi.UI.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using Icons = Microsoft.FluentUI.AspNetCore.Components.Icons;

namespace Deosrc.Tammi.UI.Tests.Components;

public sealed class ToggleButtonGroupTests : BunitContext
{
    private static readonly IReadOnlyList<ToggleButtonOption<string>> Options =
    [
        new("list", new Icons.Regular.Size16.Add(), "As a list"),
        new("tree", new Icons.Regular.Size16.Add(), "As a tree"),
    ];

    public ToggleButtonGroupTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddFluentUIComponents();
    }

    [Fact]
    public void PressesOnlyTheButtonOfTheValue()
    {
        var group = Render<ToggleButtonGroup<string>>(p => p
            .Add(g => g.Options, Options).Add(g => g.Value, "tree").Add(g => g.AriaLabel, "View"));

        var buttons = group.FindAll("fluent-button");
        Assert.Equal(["false", "true"], buttons.Select(button => button.GetAttribute("aria-pressed")));
        Assert.Equal(["As a list", "As a tree"], buttons.Select(button => button.GetAttribute("aria-label")));
        Assert.Equal("View", group.Find("[role=group]").GetAttribute("aria-label"));
    }

    [Fact]
    public void ClickingAButtonRaisesValueChangedWithItsValue()
    {
        string? chosen = null;
        var group = Render<ToggleButtonGroup<string>>(p => p
            .Add(g => g.Options, Options).Add(g => g.Value, "list").Add(g => g.AriaLabel, "View")
            .Add(g => g.ValueChanged, (string value) => chosen = value));

        group.FindAll("fluent-button")[1].Click();

        Assert.Equal("tree", chosen);
    }
}
