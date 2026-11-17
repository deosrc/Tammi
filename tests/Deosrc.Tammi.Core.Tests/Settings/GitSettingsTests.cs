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

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(501)]
    public void Normalize_SubjectLimitOutOfRange_IsTheDefault(int limit)
    {
        Assert.Equal(GitSettings.DefaultSubjectLimit, new GitSettings { SubjectLimit = limit }.Normalize().SubjectLimit);
    }

    [Fact]
    public void Normalize_SubjectLimitInRange_IsKept()
    {
        Assert.Equal(50, new GitSettings { SubjectLimit = 50 }.Normalize().SubjectLimit);
        Assert.Equal(72, new GitSettings().SubjectLimit);
    }

    [Fact]
    public void Normalize_DiffSettingsOutOfRange_AreTheDefaults()
    {
        var settings = new GitSettings { DiffTabWidth = 0, DiffContextLines = -1, DiffLayout = (GitDiffLayout)9, DiffWhitespace = (Deosrc.Tammi.Core.Git.GitDiffWhitespace)9 }.Normalize();

        Assert.Equal(GitSettings.DefaultDiffTabWidth, settings.DiffTabWidth);
        Assert.Equal(3, settings.DiffContextLines);
        Assert.Equal(GitDiffLayout.Unified, settings.DiffLayout);
        Assert.Equal(Deosrc.Tammi.Core.Git.GitDiffWhitespace.Show, settings.DiffWhitespace);
    }

    [Fact]
    public void Equals_ComparesTheGraphScopeAndFileView()
    {
        Assert.NotEqual(new GitSettings(), new GitSettings { GraphScope = GitGraphScope.Everything });
        Assert.NotEqual(new GitSettings(), new GitSettings { GraphFileView = GitGraphFileView.Tree });
        Assert.NotEqual(new GitSettings(), new GitSettings { SubjectLimit = 50 });
    }
}
