using Deosrc.Tammi.Core.Integrations;
using Deosrc.Tammi.Core.Workspaces;

namespace Deosrc.Tammi.Core.Tests.Workspaces;

public sealed class WorkspaceSearchTests
{
    private static WorkspaceListing Listing(string name = "Login page fix") =>
        new("ws1", name, "/work/login", WorkspaceAvailability.Available)
        {
            Workspace = new Workspace
            {
                Id = "ws1",
                Name = name,
                Folder = "/work/login",
                Tasks = [new TaskReference("github", "acme/api#42"), new TaskReference("jira", "ABC-123")],
                Repositories = [new WorkspaceRepository("api", "/repos/api", "api-42-login-page", "origin/main")],
            },
        };

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_text_matches_every_workspace(string? text) =>
        Assert.True(WorkspaceSearch.Matches(Listing(), text));

    [Theory]
    [InlineData("login")]
    [InlineData("LOGIN")]
    [InlineData("acme/api#42")]
    [InlineData("abc-123")]
    [InlineData("api")]
    [InlineData("api-42-login")]
    public void Matches_name_task_keys_repository_folders_and_branches(string text) =>
        Assert.True(WorkspaceSearch.Matches(Listing(), text));

    [Fact]
    public void Every_word_must_match_somewhere() =>
        Assert.True(WorkspaceSearch.Matches(Listing(), "  fix  ABC-123 "));

    [Theory]
    [InlineData("signup")]
    [InlineData("login signup")]
    [InlineData("origin/main")]
    [InlineData("/work")]
    public void Doesnt_match_other_text(string text) =>
        Assert.False(WorkspaceSearch.Matches(Listing(), text));

    [Fact]
    public void Unreadable_workspace_is_matched_by_name_only()
    {
        var listing = new WorkspaceListing("ws2", "Broken one", "/work/broken", WorkspaceAvailability.Missing);

        Assert.True(WorkspaceSearch.Matches(listing, "broken"));
        Assert.False(WorkspaceSearch.Matches(listing, "work"));
    }
}
