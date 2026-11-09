using Deosrc.Tammi.Core.Git;

namespace Deosrc.Tammi.Core.Tests.Git;

public sealed class GitSyncTests : IDisposable
{
    private readonly TempRepositories _repos = new();
    private readonly GitSync _sync;
    private readonly GitRepositoryReader _reader = new();

    public GitSyncTests() => _sync = new GitSync(_repos.Git);

    public void Dispose() => _repos.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // Two clones of one remote, so one can push what the other then fetches.
    private async Task<(string Remote, string Mine, string Other)> TwoClonesAsync()
    {
        string remote = await _repos.CreateRemoteAsync("origin");
        return (remote, await _repos.CloneAsync(remote, "mine"), await _repos.CloneAsync(remote, "other"));
    }

    [Fact]
    public async Task FetchAsync_DownloadsNewCommits_AndUpdatesBehindCount()
    {
        var (_, mine, other) = await TwoClonesAsync();
        await _repos.CommitFileAsync(other, "a.txt", "a", "Add a");
        await _repos.RunAsync(other, "push");
        Assert.Equal(0, _reader.ReadStatus(mine).BehindBy);

        await _sync.FetchAsync(mine, cancellationToken: Token);

        RepositoryStatus status = _reader.ReadStatus(mine);
        Assert.Equal(1, status.BehindBy);
        Assert.False(File.Exists(Path.Combine(mine, "a.txt")));
    }

    [Fact]
    public async Task FetchAsync_ReportsProgress()
    {
        var (_, mine, other) = await TwoClonesAsync();
        await _repos.CommitFileAsync(other, "a.txt", "a", "Add a");
        await _repos.RunAsync(other, "push");
        var lines = new List<string>();

        await _sync.FetchAsync(mine, new Collecting(lines), Token);

        Assert.NotEmpty(lines);
    }

    [Fact]
    public async Task FetchAsync_FetchesCheckedOutSubmodules()
    {
        string remote = await _repos.CreateRemoteWithSubmoduleAsync();
        string app = await _repos.CloneAsync(remote, "app", "--recurse-submodules");
        string lib = Path.Combine(app, "lib");
        string libWork = await _repos.CloneAsync(_repos.PathTo("lib.git"), "lib-work");
        await _repos.CommitFileAsync(libWork, "x.txt", "x", "Add x");
        await _repos.RunAsync(libWork, "push");

        await _sync.FetchAsync(app, cancellationToken: Token);

        GitCommandResult count = await _repos.RunAsync(lib, "rev-list", "--count", "HEAD..origin/main");
        Assert.Equal("1", count.StandardOutput.Trim());
    }

    [Fact]
    public async Task FetchAsync_InASubmodule_FetchesIt()
    {
        string remote = await _repos.CreateRemoteWithSubmoduleAsync();
        string app = await _repos.CloneAsync(remote, "app", "--recurse-submodules");
        string lib = Path.Combine(app, "lib");
        string libWork = await _repos.CloneAsync(_repos.PathTo("lib.git"), "lib-work");
        await _repos.CommitFileAsync(libWork, "x.txt", "x", "Add x");
        await _repos.RunAsync(libWork, "push");

        await _sync.FetchAsync(lib, cancellationToken: Token);

        GitCommandResult count = await _repos.RunAsync(lib, "rev-list", "--count", "HEAD..origin/main");
        Assert.Equal("1", count.StandardOutput.Trim());
    }

    [Fact]
    public async Task FetchAsync_UnreachableRemote_ThrowsNetworkFailure()
    {
        var (_, mine, _) = await TwoClonesAsync();
        await _repos.RunAsync(mine, "remote", "set-url", "origin", _repos.PathTo("missing.git"));

        GitSyncException ex = await Assert.ThrowsAsync<GitSyncException>(() => _sync.FetchAsync(mine, cancellationToken: Token));

        Assert.Equal(GitSyncFailure.Network, ex.Failure);
        Assert.Equal(GitSyncOperation.Fetch, ex.Operation);
        Assert.NotEmpty(ex.Details);
    }

    [Fact]
    public async Task PullAsync_FastForwards_AndUpdatesStatus()
    {
        var (_, mine, other) = await TwoClonesAsync();
        await _repos.CommitFileAsync(other, "a.txt", "a", "Add a");
        await _repos.RunAsync(other, "push");

        await _sync.PullAsync(mine, cancellationToken: Token);

        Assert.True(File.Exists(Path.Combine(mine, "a.txt")));
        RepositoryStatus status = _reader.ReadStatus(mine);
        Assert.Equal(0, status.BehindBy);
        Assert.Equal(0, status.AheadBy);
    }

    [Fact]
    public async Task PullAsync_DivergedBranches_MergesWhenGitConfigSaysNothing()
    {
        var (_, mine, other) = await TwoClonesAsync();
        await _repos.CommitFileAsync(other, "a.txt", "a", "Add a");
        await _repos.RunAsync(other, "push");
        await _repos.CommitFileAsync(mine, "b.txt", "b", "Add b");

        await _sync.PullAsync(mine, cancellationToken: Token);

        Assert.True(File.Exists(Path.Combine(mine, "a.txt")));
        Assert.True(File.Exists(Path.Combine(mine, "b.txt")));
        Assert.Equal(0, _reader.ReadStatus(mine).BehindBy);
        Assert.Equal(2, _reader.ReadStatus(mine).AheadBy);
    }

    [Fact]
    public async Task PullAsync_ConflictingChanges_ThrowsConflict()
    {
        var (_, mine, other) = await TwoClonesAsync();
        await _repos.CommitFileAsync(other, "README.md", "theirs\n", "Theirs");
        await _repos.RunAsync(other, "push");
        await _repos.CommitFileAsync(mine, "README.md", "mine\n", "Mine");

        GitSyncException ex = await Assert.ThrowsAsync<GitSyncException>(() => _sync.PullAsync(mine, cancellationToken: Token));

        Assert.Equal(GitSyncFailure.Conflict, ex.Failure);
        Assert.Contains(_reader.ReadStatus(mine).Changes, change => change.IsConflicted);
    }

    [Fact]
    public async Task PullAsync_UncommittedChangesWouldBeOverwritten_ThrowsLocalChanges()
    {
        var (_, mine, other) = await TwoClonesAsync();
        await _repos.CommitFileAsync(other, "README.md", "theirs\n", "Theirs");
        await _repos.RunAsync(other, "push");
        TempRepositories.WriteFile(mine, "README.md", "mine\n");

        GitSyncException ex = await Assert.ThrowsAsync<GitSyncException>(() => _sync.PullAsync(mine, cancellationToken: Token));

        Assert.Equal(GitSyncFailure.LocalChanges, ex.Failure);
    }

    [Fact]
    public async Task PullAsync_OnlyFastForwardConfigured_DivergedThrowsDiverged()
    {
        var (_, mine, other) = await TwoClonesAsync();
        await _repos.RunAsync(mine, "config", "pull.ff", "only");
        await _repos.CommitFileAsync(other, "a.txt", "a", "Add a");
        await _repos.RunAsync(other, "push");
        await _repos.CommitFileAsync(mine, "b.txt", "b", "Add b");

        GitSyncException ex = await Assert.ThrowsAsync<GitSyncException>(() => _sync.PullAsync(mine, cancellationToken: Token));

        Assert.Equal(GitSyncFailure.Diverged, ex.Failure);
    }

    [Fact]
    public async Task PullAsync_DetachedHead_ThrowsDetachedHead()
    {
        var (_, mine, _) = await TwoClonesAsync();
        await _repos.RunAsync(mine, "checkout", "--detach");

        GitSyncException ex = await Assert.ThrowsAsync<GitSyncException>(() => _sync.PullAsync(mine, cancellationToken: Token));

        Assert.Equal(GitSyncFailure.DetachedHead, ex.Failure);
    }

    [Fact]
    public async Task PushAsync_UploadsCommits_AndUpdatesAheadCount()
    {
        var (_, mine, other) = await TwoClonesAsync();
        await _repos.CommitFileAsync(mine, "a.txt", "a", "Add a");
        Assert.Equal(1, _reader.ReadStatus(mine).AheadBy);

        await _sync.PushAsync(mine, cancellationToken: Token);

        Assert.Equal(0, _reader.ReadStatus(mine).AheadBy);
        Assert.Equal(0, _reader.ReadStatus(mine).UnpushedCommitCount);
        await _sync.PullAsync(other, cancellationToken: Token);
        Assert.True(File.Exists(Path.Combine(other, "a.txt")));
    }

    [Fact]
    public async Task PushAsync_NewBranch_SetsTheUpstream()
    {
        var (_, mine, _) = await TwoClonesAsync();
        await _repos.RunAsync(mine, "checkout", "-b", "feature");
        await _repos.CommitFileAsync(mine, "a.txt", "a", "Add a");

        await _sync.PushAsync(mine, cancellationToken: Token);

        RepositoryStatus status = _reader.ReadStatus(mine);
        Assert.Equal("origin/feature", status.Upstream);
        Assert.Equal(0, status.UnpushedCommitCount);
    }

    [Fact]
    public async Task PushAsync_RemoteHasNewerCommits_ThrowsPushRejected()
    {
        var (_, mine, other) = await TwoClonesAsync();
        await _repos.CommitFileAsync(other, "a.txt", "a", "Add a");
        await _repos.RunAsync(other, "push");
        await _repos.CommitFileAsync(mine, "b.txt", "b", "Add b");

        GitSyncException ex = await Assert.ThrowsAsync<GitSyncException>(() => _sync.PushAsync(mine, cancellationToken: Token));

        Assert.Equal(GitSyncFailure.PushRejected, ex.Failure);
        Assert.Contains("Pull first", ex.Message);
    }

    [Fact]
    public async Task PushAsync_NoRemote_ThrowsNoRemote()
    {
        string repo = _repos.PathTo("local");
        await _repos.RunAsync(_repos.Root, "init", repo);
        await _repos.CommitFileAsync(repo, "a.txt", "a", "Add a");

        GitSyncException ex = await Assert.ThrowsAsync<GitSyncException>(() => _sync.PushAsync(repo, cancellationToken: Token));

        Assert.Equal(GitSyncFailure.NoRemote, ex.Failure);
    }

    [Theory]
    [InlineData("fatal: Authentication failed for 'https://example.com/x.git/'", GitSyncFailure.Authentication)]
    [InlineData("fatal: could not read Username for 'https://example.com': terminal prompts disabled", GitSyncFailure.Authentication)]
    [InlineData("git@example.com: Permission denied (publickey).\nfatal: Could not read from remote repository.", GitSyncFailure.Authentication)]
    [InlineData("fatal: unable to access 'https://example.com/x.git/': Could not resolve host: example.com", GitSyncFailure.Network)]
    [InlineData("fatal: something else entirely", GitSyncFailure.Other)]
    public void Classify_RecognisesFailuresFromGitsMessages(string error, GitSyncFailure expected)
    {
        var result = new GitCommandResult(["fetch"], "/x", 128, "", error);

        GitSyncException ex = GitSyncErrors.Classify(GitSyncOperation.Fetch, result);

        Assert.Equal(expected, ex.Failure);
    }

    private sealed class Collecting(List<string> lines) : IProgress<string>
    {
        public void Report(string value) => lines.Add(value);
    }
}
