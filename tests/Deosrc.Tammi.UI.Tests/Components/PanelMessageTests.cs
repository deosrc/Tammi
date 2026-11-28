using Bunit;
using Deosrc.Tammi.UI.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Deosrc.Tammi.UI.Tests.Components;

public sealed class PanelMessageTests : BunitContext
{
    public PanelMessageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddFluentUIComponents();
        Services.AddScoped<Clipboard>();
    }

    [Fact]
    public void ShowsItsMessageTitleAndButtons()
    {
        RenderFragment actions = builder => builder.AddMarkupContent(0, "<button class=\"test-action\">Drop</button>");
        var message = Render<PanelMessage>(parameters => parameters
            .Add(p => p.Intent, MessageBarIntent.Warning)
            .Add(p => p.Title, "Drop stash?")
            .Add(p => p.Actions, actions)
            .AddChildContent("It can't be brought back."));

        Assert.Contains("It can't be brought back.", message.Markup);
        Assert.Contains("Drop stash?", message.Markup);
        Assert.Single(message.FindAll(".test-action"));
    }

    [Fact]
    public void CanBeDismissedOnlyWithAnOnDismiss()
    {
        var without = Render<PanelMessage>(parameters => parameters.AddChildContent("Failed"));
        Assert.DoesNotContain("dismiss", without.Markup, StringComparison.OrdinalIgnoreCase);

        var with = Render<PanelMessage>(parameters => parameters
            .Add(p => p.OnDismiss, () => { })
            .AddChildContent("Failed"));
        Assert.Contains("dismiss", with.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AnErrorCopiesWhenClicked()
    {
        var error = Render<PanelMessage>(parameters => parameters.AddChildContent("Couldn't read the diff."));
        Assert.Equal("Couldn't read the diff.", error.Find(".copy-on-click").TextContent);

        var notCopied = Render<PanelMessage>(parameters => parameters
            .Add(p => p.CopyOnClick, false)
            .AddChildContent("Couldn't push."));
        Assert.Empty(notCopied.FindAll(".copy-on-click"));

        var warning = Render<PanelMessage>(parameters => parameters
            .Add(p => p.Intent, MessageBarIntent.Warning)
            .AddChildContent("Discard the changes?"));
        Assert.Empty(warning.FindAll(".copy-on-click"));
    }
}
