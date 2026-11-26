namespace Deosrc.Tammi.UI.Tests;

public sealed class AppRoutesTests
{
    [Fact]
    public void WorkspaceTab_IsUnderTheWorkspacesPath()
    {
        Assert.Equal("workspaces/w 1/tabs/7".Replace(" ", "%20"), AppRoutes.WorkspaceTab("w 1", "7"));
    }

    [Fact]
    public void WorkspaceIdOf_FindsTheWorkspaceOfAMainAreaTab()
    {
        var path = AppRoutes.WorkspaceTab("w/1", "42");

        Assert.Equal("w/1", AppRoutes.WorkspaceIdOf(path));
        Assert.Equal("42", AppRoutes.WorkspaceTabIdOf(path));
    }

    [Fact]
    public void WorkspaceTabIdOf_IsNullForTheDashboardAndOtherPaths()
    {
        Assert.Null(AppRoutes.WorkspaceTabIdOf(AppRoutes.Workspace("w1")));
        Assert.Null(AppRoutes.WorkspaceTabIdOf(AppRoutes.EditWorkspace("w1")));
        Assert.Null(AppRoutes.WorkspaceTabIdOf(AppRoutes.Home));
        Assert.Null(AppRoutes.WorkspaceTabIdOf(AppRoutes.RepositoryGit("/src/api")));
    }

    [Theory]
    [InlineData("workspaces/w1/other/7")]
    [InlineData("workspaces/w1/tabs/")]
    [InlineData("workspaces/w1/tabs/7/more")]
    [InlineData("workspaces//tabs/7")]
    public void WorkspaceIdOf_IsNullForMalformedTabPaths(string path)
    {
        Assert.Null(AppRoutes.WorkspaceIdOf(path));
        Assert.Null(AppRoutes.WorkspaceTabIdOf(path));
    }

    [Fact]
    public void WorkspaceIdOf_StillFindsTheDashboardAndEditFlow()
    {
        Assert.Equal("w1", AppRoutes.WorkspaceIdOf(AppRoutes.Workspace("w1")));
        Assert.Equal("w1", AppRoutes.WorkspaceIdOf(AppRoutes.Workspace("w1") + "/"));
        Assert.Equal("w1", AppRoutes.WorkspaceIdOf(AppRoutes.EditWorkspace("w1")));
    }

    [Fact]
    public void EditWorkspace_HasNoTabs()
    {
        Assert.Null(AppRoutes.WorkspaceIdOf(AppRoutes.EditWorkspace("w1") + "/tabs/7"));
    }

    [Fact]
    public void RepositoryGitPathOf_FindsTheRepositoryOfAMainAreaTab()
    {
        var path = AppRoutes.RepositoryGitTab("/home/dale/src/api", "9");

        Assert.Equal("/home/dale/src/api", AppRoutes.RepositoryGitPathOf(path));
        Assert.Equal("9", AppRoutes.RepositoryGitTabIdOf(path));
    }

    [Fact]
    public void RepositoryGitTabIdOf_IsNullForTheRepositorysOwnGraphAndOtherPaths()
    {
        Assert.Null(AppRoutes.RepositoryGitTabIdOf(AppRoutes.RepositoryGit("/src/api")));
        Assert.Null(AppRoutes.RepositoryGitTabIdOf(AppRoutes.Repositories));
        Assert.Null(AppRoutes.RepositoryGitTabIdOf(AppRoutes.Workspace("w1")));
        Assert.Equal("/src/api", AppRoutes.RepositoryGitPathOf(AppRoutes.RepositoryGit("/src/api")));
    }

    [Theory]
    [InlineData("repositories/git/")]
    [InlineData("repositories/git/%2Fsrc/other/9")]
    [InlineData("repositories/git/%2Fsrc/tabs/")]
    public void RepositoryGitPathOf_IsNullForMalformedPaths(string path)
    {
        Assert.Null(AppRoutes.RepositoryGitPathOf(path));
    }
}
