using Bunit;
using Deosrc.Tammi.UI.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Deosrc.Tammi.UI.Tests.Components;

public sealed class AutofocusTextInputTests : BunitContext
{
    private const string Focus = "Microsoft.FluentUI.Blazor.Utilities.DomFunctions.focus";

    public AutofocusTextInputTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddFluentUIComponents();
    }

    [Fact]
    public void IgnoresItsElementBeingGoneWhenFocused()
    {
        JSInterop.SetupVoid(Focus, _ => true).SetException(new JSException("Unable to focus an invalid element."));

        var input = Render<AutofocusTextInput>(parameters => parameters.Add(p => p.Autofocus, true));

        Assert.Single(JSInterop.Invocations[Focus]);
        Assert.Single(input.FindAll("fluent-text-input"));
    }

    [Fact]
    public void LetsOtherFocusFailuresThrough()
    {
        JSInterop.SetupVoid(Focus, _ => true).SetException(new JSException("Something else went wrong."));

        var ex = Assert.ThrowsAny<Exception>(() =>
            Render<AutofocusTextInput>(parameters => parameters.Add(p => p.Autofocus, true)));

        Assert.Contains("Something else went wrong.", ex.ToString());
    }
}
