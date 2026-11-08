using Deosrc.Tammi.Core.Settings;

namespace Deosrc.Tammi.Core.Tests.Settings;

public sealed class GitSettingsTests
{
    [Fact]
    public void Normalize_UnknownGraphScope_IsTheWorkspaceBranch()
    {
        var settings = new GitSettings { GraphScope = (GitGraphScope)42 }.Normalize();

        Assert.Equal(GitGraphScope.WorkspaceBranch, settings.GraphScope);
    }

    [Fact]
    public void Normalize_UnknownGraphFileView_IsTheList()
    {
        var settings = new GitSettings { GraphFileView = (GitGraphFileView)42 }.Normalize();

        Assert.Equal(GitGraphFileView.List, settings.GraphFileView);
    }

    [Fact]
    public void Equals_ComparesTheGraphScopeAndFileView()
    {
        Assert.NotEqual(new GitSettings(), new GitSettings { GraphScope = GitGraphScope.Everything });
        Assert.NotEqual(new GitSettings(), new GitSettings { GraphFileView = GitGraphFileView.Tree });
    }
}
