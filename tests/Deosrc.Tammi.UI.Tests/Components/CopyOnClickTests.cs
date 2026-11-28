using Bunit;
using Deosrc.Tammi.UI.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Deosrc.Tammi.UI.Tests.Components;

public sealed class CopyOnClickTests : BunitContext
{
    private readonly BunitJSModuleInterop _clipboard;

    public CopyOnClickTests()
    {
        Services.AddScoped<Clipboard>();
        _clipboard = JSInterop.SetupModule("./_content/Deosrc.Tammi.UI/js/clipboard.js");
        _clipboard.Setup<bool>("copyText", _ => true).SetResult(true);
        _clipboard.Setup<bool>("copyElementText", _ => true).SetResult(true);
    }

    [Fact]
    public void CopiesTheTextItShows_WhenClicked()
    {
        var copy = Render<CopyOnClick>(parameters => parameters.AddChildContent("Couldn't read the diff."));

        copy.Find("span.copy-on-click").Click();

        Assert.Single(_clipboard.Invocations["copyElementText"]);
        Assert.Empty(_clipboard.Invocations["copyText"]);
    }

    [Fact]
    public void CopiesItsText_WhenGivenOne()
    {
        var copy = Render<CopyOnClick>(parameters => parameters
            .Add(p => p.Text, "fatal: not a git repository")
            .AddChildContent("Couldn't read the status."));

        copy.Find(".copy-on-click").Click();

        var invocation = Assert.Single(_clipboard.Invocations["copyText"]);
        Assert.Equal("fatal: not a git repository", invocation.Arguments[0]);
    }

    [Theory]
    [InlineData("Enter", 1)]
    [InlineData(" ", 1)]
    [InlineData("a", 0)]
    public void CopiesWithEnterOrSpace(string key, int copies)
    {
        var copy = Render<CopyOnClick>(parameters => parameters.Add(p => p.Text, "read:jira-work"));

        copy.Find(".copy-on-click").KeyDown(new KeyboardEventArgs { Key = key });

        Assert.Equal(copies, _clipboard.Invocations["copyText"].Count);
    }

    [Fact]
    public void SaysItWasCopied()
    {
        var copy = Render<CopyOnClick>(parameters => parameters.Add(p => p.Text, "read:jira-work"));
        Assert.Equal("", copy.Find("[role=status]").TextContent);

        copy.Find(".copy-on-click").Click();

        copy.WaitForAssertion(() =>
        {
            Assert.Equal("Copied", copy.Find("[role=status]").TextContent);
            Assert.Contains("copied", copy.Find(".copy-on-click").ClassList);
        });
    }

    [Fact]
    public void IsABlockWhenAskedAndNamesWhatItCopies()
    {
        var copy = Render<CopyOnClick>(parameters => parameters
            .Add(p => p.Block, true)
            .Add(p => p.Class, "license-text")
            .Add(p => p.What, "the license")
            .AddChildContent("MIT License"));

        var element = copy.Find("div.copy-on-click.license-text");
        Assert.Equal("Click to copy the license", element.GetAttribute("title"));
        Assert.Equal("button", element.GetAttribute("role"));
        Assert.Equal("MIT License", element.TextContent);
    }
}
