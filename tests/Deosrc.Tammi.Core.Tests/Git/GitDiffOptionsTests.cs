using Deosrc.Tammi.Core.Git;

namespace Deosrc.Tammi.Core.Tests.Git;

public sealed class GitDiffOptionsTests
{
    [Theory]
    [InlineData(GitDiffWhitespace.Show, false, new[] { "-U5" })]
    [InlineData(GitDiffWhitespace.IgnoreAmount, false, new[] { "-U5", "-b" })]
    [InlineData(GitDiffWhitespace.IgnoreAll, false, new[] { "-U5", "-w" })]
    [InlineData(GitDiffWhitespace.IgnoreAtEol, false, new[] { "-U5", "--ignore-space-at-eol" })]
    [InlineData(GitDiffWhitespace.IgnoreAll, true, new[] { "-U5", "-w", "--ignore-blank-lines" })]
    [InlineData(GitDiffWhitespace.Show, true, new[] { "-U5", "--ignore-blank-lines" })]
    public void ToArguments_PassesTheWhitespaceModeToGit(GitDiffWhitespace whitespace, bool blank, string[] expected) =>
        Assert.Equal(expected, new GitDiffOptions(whitespace, blank).ToArguments());

    [Fact]
    public void ToArguments_UsesTheContextLines() =>
        Assert.Equal("-U0", new GitDiffOptions(ContextLines: 0).ToArguments()[0]);

    [Fact]
    public void NeedsCli_OnlyWhenSomethingIsIgnored()
    {
        Assert.False(new GitDiffOptions().NeedsCli);
        Assert.True(new GitDiffOptions(GitDiffWhitespace.IgnoreAll).NeedsCli);
        Assert.True(new GitDiffOptions(IgnoreBlankLines: true).NeedsCli);
    }
}
