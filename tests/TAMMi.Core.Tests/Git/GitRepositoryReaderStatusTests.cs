using TAMMi.Core.Git;

namespace TAMMi.Core.Tests.Git;

public sealed class GitRepositoryReaderStatusTests : IDisposable
{
    private readonly TempRepositories _repos = new();
    private readonly GitRepositoryReader _reader = new();

    public void Dispose() => _repos.Dispose();

    [Fact]
    public async Task Clean_IsNotChanged()
    {
        string clone = await CloneAsync();

        RepositoryStatus status = _reader.ReadStatus(clone);

        Assert.Equal(clone, status.Path);
        Assert.Equal("main", status.Branch);
        Assert.Equal("origin/main", status.Upstream);
        Assert.Equal(0, status.AheadBy);
        Assert.Equal(0, status.BehindBy);
        Assert.Equal(0, status.UnpushedCommitCount);
        Assert.NotNull(status.HeadSha);
        Assert.Empty(status.Changes);
        Assert.Empty(status.Submodules);
        Assert.False(status.HasUncommittedChanges);
        Assert.False(status.HasUnpushedCommits);
        Assert.False(status.IsChanged);
        Assert.False(status.IsBehind);
    }

    [Fact]
    public async Task UntrackedFile_IsChanged()
    {
        string clone = await CloneAsync();
        TempRepositories.WriteFile(clone, "src/new.txt", "new");

        RepositoryStatus status = _reader.ReadStatus(clone);

        FileChange change = Assert.Single(status.Changes);
        Assert.Equal(new FileChange("src/new.txt", FileChangeKind.None, FileChangeKind.Untracked), change);
        Assert.True(change.IsUntracked);
        Assert.True(change.IsUnstaged);
        Assert.False(change.IsStaged);
        AssertChangedByUncommittedChangesOnly(status);
    }

    [Fact]
    public async Task IgnoredFile_IsNotChanged()
    {
        string remote = await _repos.CreateRemoteAsync("repo");
        string clone = await _repos.CloneAsync(remote, "clone");
        await _repos.CommitFileAsync(clone, ".gitignore", "bin/\n", "Ignore bin");
        await _repos.RunAsync(clone, "push");
        TempRepositories.WriteFile(clone, "bin/output.dll", "binary");

        RepositoryStatus status = _reader.ReadStatus(clone);

        Assert.Empty(status.Changes);
        Assert.False(status.IsChanged);
    }

    [Fact]
    public async Task UnstagedModification_IsChanged()
    {
        string clone = await CloneAsync();
        TempRepositories.WriteFile(clone, "README.md", "changed\n");

        RepositoryStatus status = _reader.ReadStatus(clone);

        FileChange change = Assert.Single(status.Changes);
        Assert.Equal(new FileChange("README.md", FileChangeKind.None, FileChangeKind.Modified), change);
        Assert.True(change.IsUnstaged);
        Assert.False(change.IsStaged);
        AssertChangedByUncommittedChangesOnly(status);
    }

    [Fact]
    public async Task UnstagedDeletion_IsChanged()
    {
        string clone = await CloneAsync();
        File.Delete(Path.Combine(clone, "README.md"));

        RepositoryStatus status = _reader.ReadStatus(clone);

        Assert.Equal(
            [new FileChange("README.md", FileChangeKind.None, FileChangeKind.Deleted)],
            status.Changes);
        AssertChangedByUncommittedChangesOnly(status);
    }

    [Fact]
    public async Task StagedNewFile_IsChanged()
    {
        string clone = await CloneAsync();
        TempRepositories.WriteFile(clone, "new.txt", "new");
        await _repos.RunAsync(clone, "add", "new.txt");

        RepositoryStatus status = _reader.ReadStatus(clone);

        FileChange change = Assert.Single(status.Changes);
        Assert.Equal(new FileChange("new.txt", FileChangeKind.Added, FileChangeKind.None), change);
        Assert.True(change.IsStaged);
        Assert.False(change.IsUnstaged);
        AssertChangedByUncommittedChangesOnly(status);
    }

    [Fact]
    public async Task StagedModificationWithFurtherUnstagedChanges_ReportsBoth()
    {
        string clone = await CloneAsync();
        TempRepositories.WriteFile(clone, "README.md", "staged\n");
        await _repos.RunAsync(clone, "add", "README.md");
        TempRepositories.WriteFile(clone, "README.md", "staged and unstaged\n");

        RepositoryStatus status = _reader.ReadStatus(clone);

        FileChange change = Assert.Single(status.Changes);
        Assert.Equal(new FileChange("README.md", FileChangeKind.Modified, FileChangeKind.Modified), change);
        Assert.True(change.IsStaged);
        Assert.True(change.IsUnstaged);
        AssertChangedByUncommittedChangesOnly(status);
    }

    [Fact]
    public async Task StagedDeletion_IsChanged()
    {
        string clone = await CloneAsync();
        await _repos.RunAsync(clone, "rm", "README.md");

        RepositoryStatus status = _reader.ReadStatus(clone);

        Assert.Equal(
            [new FileChange("README.md", FileChangeKind.Deleted, FileChangeKind.None)],
            status.Changes);
        AssertChangedByUncommittedChangesOnly(status);
    }

    [Fact]
    public async Task StagedRename_ReportsOldPath()
    {
        string clone = await CloneAsync();
        await _repos.RunAsync(clone, "mv", "README.md", "OVERVIEW.md");

        RepositoryStatus status = _reader.ReadStatus(clone);

        Assert.Equal(
            [new FileChange("OVERVIEW.md", FileChangeKind.Renamed, FileChangeKind.None, "README.md")],
            status.Changes);
        AssertChangedByUncommittedChangesOnly(status);
    }

    [Fact]
    public async Task MergeConflict_IsChanged()
    {
        string clone = await CloneAsync();
        await _repos.RunAsync(clone, "switch", "-c", "other");
        await _repos.CommitFileAsync(clone, "README.md", "other\n", "Change on other");
        await _repos.RunAsync(clone, "switch", "main");
        await _repos.CommitFileAsync(clone, "README.md", "main\n", "Change on main");
        await _repos.Git.RunAsync(
            clone,
            ["merge", "other"],
            new GitCommandOptions { ThrowOnError = false },
            TestContext.Current.CancellationToken);

        RepositoryStatus status = _reader.ReadStatus(clone);

        FileChange change = Assert.Single(status.Changes);
        Assert.Equal(new FileChange("README.md", FileChangeKind.Conflicted, FileChangeKind.Conflicted), change);
        Assert.True(change.IsConflicted);
        Assert.True(status.HasUncommittedChanges);
        Assert.True(status.IsChanged);
    }

    [Fact]
    public async Task UnpushedCommit_IsChanged()
    {
        string clone = await CloneAsync();
        await _repos.CommitFileAsync(clone, "new.txt", "new", "Local commit");

        RepositoryStatus status = _reader.ReadStatus(clone);

        Assert.Empty(status.Changes);
        Assert.Equal(1, status.AheadBy);
        Assert.Equal(0, status.BehindBy);
        Assert.Equal(1, status.UnpushedCommitCount);
        Assert.False(status.HasUncommittedChanges);
        Assert.True(status.HasUnpushedCommits);
        Assert.True(status.IsChanged);
    }

    [Fact]
    public async Task PushedCommit_IsNotChanged()
    {
        string clone = await CloneAsync();
        await _repos.CommitFileAsync(clone, "new.txt", "new", "Pushed commit");
        await _repos.RunAsync(clone, "push");

        RepositoryStatus status = _reader.ReadStatus(clone);

        Assert.Equal(0, status.UnpushedCommitCount);
        Assert.False(status.IsChanged);
    }

    [Fact]
    public async Task NewBranchWithoutUpstream_CountsOnlyLocalCommitsAsUnpushed()
    {
        string clone = await CloneAsync();
        await _repos.RunAsync(clone, "switch", "--no-track", "-c", "feature", "origin/main");

        RepositoryStatus before = _reader.ReadStatus(clone);
        await _repos.CommitFileAsync(clone, "feature.txt", "feature", "Feature commit");
        RepositoryStatus after = _reader.ReadStatus(clone);

        Assert.Equal("feature", before.Branch);
        Assert.Null(before.Upstream);
        Assert.Null(before.AheadBy);
        Assert.Null(before.BehindBy);
        Assert.False(before.IsChanged);

        Assert.Equal(1, after.UnpushedCommitCount);
        Assert.True(after.IsChanged);
    }

    [Fact]
    public async Task BranchPushedElsewhereThanUpstream_IsNotChanged()
    {
        // A workspace branch created from origin/main tracks origin/main, so it's "ahead" of its upstream even
        // after being pushed to its own remote branch. Nothing would be lost, so it isn't changed.
        string clone = await CloneAsync();
        await _repos.RunAsync(clone, "switch", "--track", "-c", "feature", "origin/main");
        await _repos.CommitFileAsync(clone, "feature.txt", "feature", "Feature commit");
        await _repos.RunAsync(clone, "push", "origin", "feature");

        RepositoryStatus status = _reader.ReadStatus(clone);

        Assert.Equal("origin/main", status.Upstream);
        Assert.Equal(1, status.AheadBy);
        Assert.Equal(0, status.UnpushedCommitCount);
        Assert.False(status.IsChanged);
    }

    [Fact]
    public async Task Behind_IsReportedSeparatelyAndIsNotChanged()
    {
        string remote = await _repos.CreateRemoteAsync("repo");
        string clone = await _repos.CloneAsync(remote, "clone");
        string other = await _repos.CloneAsync(remote, "other");
        await _repos.CommitFileAsync(other, "new.txt", "new", "Someone else's commit");
        await _repos.RunAsync(other, "push");
        await _repos.RunAsync(clone, "fetch");

        RepositoryStatus status = _reader.ReadStatus(clone);

        Assert.Equal(0, status.AheadBy);
        Assert.Equal(1, status.BehindBy);
        Assert.True(status.IsBehind);
        Assert.False(status.IsChanged);
    }

    [Fact]
    public async Task BehindWithUnpushedCommit_IsChangedAndBehind()
    {
        string remote = await _repos.CreateRemoteAsync("repo");
        string clone = await _repos.CloneAsync(remote, "clone");
        string other = await _repos.CloneAsync(remote, "other");
        await _repos.CommitFileAsync(other, "theirs.txt", "theirs", "Their commit");
        await _repos.RunAsync(other, "push");
        await _repos.CommitFileAsync(clone, "mine.txt", "mine", "My commit");
        await _repos.RunAsync(clone, "fetch");

        RepositoryStatus status = _reader.ReadStatus(clone);

        Assert.Equal(1, status.AheadBy);
        Assert.Equal(1, status.BehindBy);
        Assert.Equal(1, status.UnpushedCommitCount);
        Assert.True(status.IsChanged);
        Assert.True(status.IsBehind);
    }

    [Fact]
    public async Task NoRemotes_CommitsAreNotUnpushed()
    {
        string repo = _repos.PathTo("local");
        await _repos.RunAsync(_repos.Root, "init", repo);
        await _repos.CommitFileAsync(repo, "file.txt", "content", "Local only");

        RepositoryStatus status = _reader.ReadStatus(repo);

        Assert.Equal(0, status.UnpushedCommitCount);
        Assert.Null(status.Upstream);
        Assert.False(status.IsChanged);
    }

    [Fact]
    public async Task EmptyRepository_IsNotChanged()
    {
        string repo = _repos.PathTo("empty");
        await _repos.RunAsync(_repos.Root, "init", repo);

        RepositoryStatus status = _reader.ReadStatus(repo);

        Assert.Equal("main", status.Branch);
        Assert.Null(status.HeadSha);
        Assert.False(status.IsChanged);
    }

    [Fact]
    public async Task DetachedHead_HasNoBranch()
    {
        string clone = await CloneAsync();
        await _repos.RunAsync(clone, "switch", "--detach", "HEAD");

        RepositoryStatus status = _reader.ReadStatus(clone);

        Assert.Null(status.Branch);
        Assert.Null(status.Upstream);
        Assert.NotNull(status.HeadSha);
        Assert.False(status.IsChanged);
    }

    [Fact]
    public async Task Worktree_ReadsWorktreeState()
    {
        string clone = await CloneAsync();
        string worktree = Path.Combine(_repos.PathTo("workspace"), "repo");
        await _repos.RunAsync(clone, "worktree", "add", "-b", "task-1", worktree, "origin/main");
        TempRepositories.WriteFile(worktree, "task.txt", "task");

        RepositoryStatus worktreeStatus = _reader.ReadStatus(worktree);
        RepositoryStatus cloneStatus = _reader.ReadStatus(clone);

        Assert.Equal(worktree, worktreeStatus.Path);
        Assert.Equal("task-1", worktreeStatus.Branch);
        Assert.Equal("task.txt", Assert.Single(worktreeStatus.Changes).Path);
        Assert.True(worktreeStatus.IsChanged);

        Assert.Equal("main", cloneStatus.Branch);
        Assert.False(cloneStatus.IsChanged);
    }

    [Fact]
    public void NotARepository_Throws()
    {
        string folder = _repos.PathTo("not-a-repo");
        Directory.CreateDirectory(folder);

        var ex = Assert.Throws<GitRepositoryNotFoundException>(() => _reader.ReadStatus(folder));

        Assert.Equal(folder, ex.Path);
    }

    private async Task<string> CloneAsync()
    {
        string remote = await _repos.CreateRemoteAsync("repo");
        return await _repos.CloneAsync(remote, "clone");
    }

    private static void AssertChangedByUncommittedChangesOnly(RepositoryStatus status)
    {
        Assert.True(status.HasUncommittedChanges);
        Assert.False(status.HasUnpushedCommits);
        Assert.True(status.IsChanged);
        Assert.False(status.IsBehind);
    }
}
