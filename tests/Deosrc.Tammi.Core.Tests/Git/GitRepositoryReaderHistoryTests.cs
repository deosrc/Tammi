using Deosrc.Tammi.Core.Git;

namespace Deosrc.Tammi.Core.Tests.Git;

public sealed class GitRepositoryReaderHistoryTests : IDisposable
{
    private readonly TempRepositories _repos = new();
    private readonly GitRepositoryReader _reader = new();

    public void Dispose() => _repos.Dispose();

    [Fact]
    public async Task ReadHistory_ReturnsCommitsNewestFirst()
    {
        string clone = await CloneAsync();
        await _repos.CommitFileAsync(clone, "a.txt", "a", "Add a\n\nWith a body.");
        await _repos.CommitFileAsync(clone, "b.txt", "b", "Add b");

        IReadOnlyList<CommitInfo> history = _reader.ReadHistory(clone);

        Assert.Equal(["Add b", "Add a", "Initial commit"], history.Select(commit => commit.Subject));
        Assert.Equal("Add a\n\nWith a body.\n", history[1].Message);
        Assert.Equal([history[1].Sha], history[0].ParentShas);
        Assert.Empty(history[2].ParentShas);
        Assert.Equal("Tammi Tests", history[0].Author.Name);
        Assert.Equal("tests@tammi.invalid", history[0].Committer.Email);

        string head = Assert.Single((await _repos.RunAsync(clone, "rev-parse", "HEAD")).OutputLines);
        Assert.Equal(head, history[0].Sha);
    }

    [Fact]
    public async Task ReadHistory_MaxCount_LimitsCommits()
    {
        string clone = await CloneAsync();
        await _repos.CommitFileAsync(clone, "a.txt", "a", "Add a");
        await _repos.CommitFileAsync(clone, "b.txt", "b", "Add b");

        IReadOnlyList<CommitInfo> history = _reader.ReadHistory(clone, new HistoryOptions { MaxCount = 2 });

        Assert.Equal(["Add b", "Add a"], history.Select(commit => commit.Subject));
    }

    [Fact]
    public async Task ReadHistory_AllRefs_IncludesOtherBranches()
    {
        string clone = await CloneAsync();
        await _repos.RunAsync(clone, "switch", "-c", "other");
        await _repos.CommitFileAsync(clone, "other.txt", "other", "On other");
        await _repos.RunAsync(clone, "switch", "main");
        await _repos.CommitFileAsync(clone, "main.txt", "main", "On main");

        IReadOnlyList<CommitInfo> headOnly = _reader.ReadHistory(clone);
        IReadOnlyList<CommitInfo> allRefs = _reader.ReadHistory(clone, new HistoryOptions { AllRefs = true });

        Assert.DoesNotContain(headOnly, commit => commit.Subject == "On other");
        Assert.Contains(allRefs, commit => commit.Subject == "On other");
        Assert.Contains(allRefs, commit => commit.Subject == "On main");
        Assert.Equal("Initial commit", allRefs[^1].Subject);
    }

    [Fact]
    public async Task ReadHistory_MergeCommit_HasTwoParents()
    {
        string clone = await CloneAsync();
        await _repos.RunAsync(clone, "switch", "-c", "other");
        await _repos.CommitFileAsync(clone, "other.txt", "other", "On other");
        await _repos.RunAsync(clone, "switch", "main");
        await _repos.CommitFileAsync(clone, "main.txt", "main", "On main");
        await _repos.RunAsync(clone, "merge", "--no-edit", "other");

        IReadOnlyList<CommitInfo> history = _reader.ReadHistory(clone);

        Assert.Equal(2, history[0].ParentShas.Count);
        Assert.Equal(4, history.Count);
    }

    [Fact]
    public async Task ReadHistory_EmptyRepository_ReturnsNoCommits()
    {
        string repo = _repos.PathTo("empty");
        await _repos.RunAsync(_repos.Root, "init", repo);

        Assert.Empty(_reader.ReadHistory(repo));
        Assert.Empty(_reader.ReadHistory(repo, new HistoryOptions { AllRefs = true }));
    }

    [Fact]
    public async Task ReadHistory_StartingPoints_WalksFromThemAndSkipsMissingNames()
    {
        string clone = await CloneAsync();
        await _repos.RunAsync(clone, "switch", "-c", "first");
        await _repos.CommitFileAsync(clone, "first.txt", "first", "On first");
        await _repos.RunAsync(clone, "switch", "-c", "second", "main");
        await _repos.CommitFileAsync(clone, "second.txt", "second", "On second");
        await _repos.RunAsync(clone, "switch", "-c", "third", "main");
        await _repos.CommitFileAsync(clone, "third.txt", "third", "On third");

        IReadOnlyList<CommitInfo> history = _reader.ReadHistory(
            clone,
            new HistoryOptions { StartingPoints = ["first", "missing", "origin/main", "second"] });

        Assert.Equal(["Initial commit", "On first", "On second"], history.Select(commit => commit.Subject).Order());
    }

    [Fact]
    public async Task ReadHistory_IncludeUpstreams_WalksFromTheBranchesUpstreams()
    {
        string clone = await CloneAsync();
        await _repos.RunAsync(clone, "switch", "-c", "other");
        await _repos.CommitFileAsync(clone, "other.txt", "other", "Pushed on other");
        await _repos.RunAsync(clone, "push", "-u", "origin", "other");
        await _repos.RunAsync(clone, "reset", "--hard", "HEAD~1");

        IReadOnlyList<CommitInfo> without = _reader.ReadHistory(
            clone,
            new HistoryOptions { StartingPoints = ["HEAD"] });
        IReadOnlyList<CommitInfo> fromHead = _reader.ReadHistory(
            clone,
            new HistoryOptions { StartingPoints = ["HEAD"], IncludeUpstreams = true });
        await _repos.RunAsync(clone, "switch", "main");
        IReadOnlyList<CommitInfo> fromName = _reader.ReadHistory(
            clone,
            new HistoryOptions { StartingPoints = ["other"], IncludeUpstreams = true });

        Assert.DoesNotContain(without, commit => commit.Subject == "Pushed on other");
        Assert.Contains(fromHead, commit => commit.Subject == "Pushed on other");
        Assert.Contains(fromName, commit => commit.Subject == "Pushed on other");
    }

    [Fact]
    public async Task OpenHistory_ReadsPagesInTheSameOrderAsOneRead()
    {
        string clone = await CloneAsync();
        await _repos.RunAsync(clone, "switch", "-c", "other");
        await _repos.CommitFileAsync(clone, "other.txt", "other", "On other");
        await _repos.RunAsync(clone, "switch", "main");
        for (int i = 0; i < 4; i++)
        {
            await _repos.CommitFileAsync(clone, $"{i}.txt", $"{i}", $"Commit {i}");
        }

        await _repos.RunAsync(clone, "merge", "--no-edit", "other");
        var options = new HistoryOptions { AllRefs = true, MaxCount = null };
        IReadOnlyList<CommitInfo> all = _reader.ReadHistory(clone, options);

        using IGitHistoryCursor cursor = _reader.OpenHistory(clone, options);
        var pages = new List<CommitInfo>();
        while (!cursor.IsComplete)
        {
            pages.AddRange(cursor.ReadNext(3));
        }

        Assert.Equal(all.Select(commit => commit.Sha), pages.Select(commit => commit.Sha));
        Assert.Empty(cursor.ReadNext(3));
    }

    [Fact]
    public async Task OpenHistory_EmptyRepository_IsComplete()
    {
        string repo = _repos.PathTo("empty");
        await _repos.RunAsync(_repos.Root, "init", repo);

        using IGitHistoryCursor cursor = _reader.OpenHistory(repo);

        Assert.True(cursor.IsComplete);
        Assert.Empty(cursor.ReadNext(10));
    }

    [Fact]
    public void OpenHistory_NoRepository_Throws()
    {
        string folder = _repos.PathTo("not-a-repository");
        Directory.CreateDirectory(folder);

        Assert.Throws<GitRepositoryNotFoundException>(() => _reader.OpenHistory(folder));
    }

    private async Task<string> CloneAsync()
    {
        string remote = await _repos.CreateRemoteAsync("repo");
        return await _repos.CloneAsync(remote, "clone");
    }
}
