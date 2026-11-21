using Bunit;
using Deosrc.Tammi.UI.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Deosrc.Tammi.UI.Tests.Components;

public sealed class CardListItemTests : BunitContext
{
    public CardListItemTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddFluentUIComponents();
    }

    [Fact]
    public void ShowsItsContentInARowInsideAListItemCard()
    {
        var item = Render<CardListItem>(parameters => parameters
            .AddChildContent("<span class=\"test-text\">A folder</span><button class=\"test-button\">Remove</button>"));

        var row = item.Find("li .fluent-card .card-list-item-row");

        Assert.Equal("A folder", row.QuerySelector(".test-text")!.TextContent);
        Assert.NotNull(row.QuerySelector(".test-button"));
    }
}
