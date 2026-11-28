using Bunit;
using Deosrc.Tammi.Core.Integrations;
using Deosrc.Tammi.UI.Workspaces;
using Microsoft.FluentUI.AspNetCore.Components;

namespace Deosrc.Tammi.UI.Tests.Components;

public sealed class TaskStatusBadgeTests : BunitContext
{
    public TaskStatusBadgeTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddFluentUIComponents();
    }

    private static TaskInfo Task(string? status, TaskStatusCategory? category) =>
        new("github", "acme/api#42", "Fix it", new Uri("https://example.test/issues/42"))
        {
            Status = status,
            StatusCategory = category,
        };

    [Theory]
    [InlineData("Todo", TaskStatusCategory.ToDo, "To do")]
    [InlineData("In review", TaskStatusCategory.InProgress, "In progress")]
    [InlineData("Not planned", TaskStatusCategory.Done, "Done")]
    public void ShowsTheStatus_WithItsCategoryAsTheHint(string status, TaskStatusCategory category, string hint)
    {
        var badge = Render<TaskStatusBadge>(p => p.Add(b => b.Task, Task(status, category)));

        var element = badge.Find("fluent-badge");
        Assert.Equal(status, element.TextContent.Trim());
        Assert.Equal(hint, element.GetAttribute("title"));
    }

    [Fact]
    public void ShowsNothing_WhenTheSourceDidntSayTheStatus()
    {
        var badge = Render<TaskStatusBadge>(p => p.Add(b => b.Task, Task(null, null)));

        Assert.Empty(badge.FindAll("fluent-badge"));
    }
}
