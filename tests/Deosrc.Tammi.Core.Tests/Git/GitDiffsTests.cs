using Deosrc.Tammi.Core.Git;

namespace Deosrc.Tammi.Core.Tests.Git;

public sealed class GitDiffsTests : IDisposable
{
    private readonly TempRepositories _repos = new();
    private readonly GitDiffs _diffs;

    public GitDiffsTests() => _diffs = new GitDiffs(new GitRepositoryReader(), _repos.Git);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _repos.Dispose();

    [Fact]
    public async Task Unstaged_WhitespaceOnlyChange_IsShownByDefaultAndHiddenWhenIgnored()
    {
        string clone = await CloneAsync();
        await _repos.CommitFileAsync(clone, "f.txt", "a b\nkeep\n", "Add f");
        TempRepositories.WriteFile(clone, "f.txt", "a   b\nkeep\n");

        FileDiff shown = await _diffs.ReadAsync(clone, GitDiffSource.Unstaged, "f.txt", new GitDiffOptions(), cancellationToken: Ct);
        FileDiff ignored = await _diffs.ReadAsync(clone, GitDiffSource.Unstaged, "f.txt", new GitDiffOptions(GitDiffWhitespace.IgnoreAmount), cancellationToken: Ct);

        Assert.Single(shown.Hunks);
        Assert.Empty(ignored.Hunks);
    }

    [Fact]
    public async Task Staged_IgnoringAllWhitespace_KeepsRealChangesWithGitsLineNumbers()
    {
        string clone = await CloneAsync();
        await _repos.CommitFileAsync(clone, "f.txt", "one\ntwo\nthree\n", "Add f");
        TempRepositories.WriteFile(clone, "f.txt", "one \nTWO\nthree\n");
        await _repos.RunAsync(clone, "add", "f.txt");

        FileDiff diff = await _diffs.ReadAsync(clone, GitDiffSource.Staged, "f.txt", new GitDiffOptions(GitDiffWhitespace.IgnoreAll), cancellationToken: Ct);

        DiffHunk hunk = Assert.Single(diff.Hunks);
        Assert.Contains(hunk.Lines, l => l is { Kind: DiffLineKind.Removed, Text: "two", OldNumber: 2 });
        Assert.Contains(hunk.Lines, l => l is { Kind: DiffLineKind.Added, Text: "TWO", NewNumber: 2 });
        Assert.DoesNotContain(hunk.Lines, l => l.Text.StartsWith("one", StringComparison.Ordinal) && l.Kind != DiffLineKind.Context);
    }

    [Fact]
    public async Task Commit_IgnoringWhitespace_HidesAWhitespaceOnlyChange()
    {
        string clone = await CloneAsync();
        await _repos.CommitFileAsync(clone, "f.txt", "a b\n", "Add f");
        await _repos.CommitFileAsync(clone, "f.txt", "a  b\n", "Spaces");
        string sha = new GitRepositoryReader().ReadHistory(clone, new HistoryOptions { MaxCount = 1 })[0].Sha;

        FileDiff diff = await _diffs.ReadAsync(clone, GitDiffSource.Commit, "f.txt", new GitDiffOptions(GitDiffWhitespace.IgnoreAll), sha: sha, cancellationToken: Ct);

        Assert.Empty(diff.Hunks);
    }

    [Fact]
    public async Task ContextLines_AreUsed()
    {
        string clone = await CloneAsync();
        await _repos.CommitFileAsync(clone, "f.txt", "1\n2\n3\n4\n5\n", "Add f");
        TempRepositories.WriteFile(clone, "f.txt", "1\n2\nX\n4\n5\n");

        FileDiff diff = await _diffs.ReadAsync(clone, GitDiffSource.Unstaged, "f.txt", new GitDiffOptions(ContextLines: 0), cancellationToken: Ct);

        Assert.Equal(2, Assert.Single(diff.Hunks).Lines.Count);
    }

    private async Task<string> CloneAsync()
    {
        string remote = await _repos.CreateRemoteAsync("repo");
        return await _repos.CloneAsync(remote, "clone");
    }
}
