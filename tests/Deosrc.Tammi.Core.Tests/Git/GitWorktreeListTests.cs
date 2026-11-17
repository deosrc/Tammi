using Deosrc.Tammi.Core.Git;

namespace Deosrc.Tammi.Core.Tests.Git;

public sealed class GitWorktreeListTests
{
    [Fact]
    public void Parse_ReadsPathsAndBranches_AndMarksTheFirstAsMain()
    {
        string[] lines =
        [
            "worktree /repos/api", "HEAD aaa", "branch refs/heads/main", "",
            "worktree /work/feature", "HEAD bbb", "branch refs/heads/feature/x", "",
            "worktree /work/detached", "HEAD ccc", "detached", "",
        ];

        Assert.Equal(
            [
                new GitWorktreeEntry("/repos/api", "main", true),
                new GitWorktreeEntry("/work/feature", "feature/x", false),
                new GitWorktreeEntry("/work/detached", null, false),
            ],
            GitWorktreeList.Parse(lines));
    }

    [Fact]
    public void Parse_WithNoOutput_ReturnsNothing() => Assert.Empty(GitWorktreeList.Parse([]));
}
