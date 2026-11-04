using Tammi.Core.Git;

namespace Tammi.Core.Tests.Git;

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

    private async Task<string> CloneAsync()
    {
        string remote = await _repos.CreateRemoteAsync("repo");
        return await _repos.CloneAsync(remote, "clone");
    }
}
