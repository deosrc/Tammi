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
    public void Equals_ComparesTheGraphScope()
    {
        Assert.NotEqual(new GitSettings(), new GitSettings { GraphScope = GitGraphScope.Everything });
    }
}
