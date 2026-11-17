using Deosrc.Tammi.Core.Git;

namespace Deosrc.Tammi.Core.Tests.Git;

public sealed class GitWorkingTreeTests : IDisposable
{
    private readonly TempRepositories _repos = new();
    private readonly GitWorkingTree _tree;
    private readonly GitRepositoryReader _reader = new();

    public GitWorkingTreeTests() => _tree = new GitWorkingTree(_repos.Git);

    public void Dispose() => _repos.Dispose();

    [Fact]
    public async Task StageAsync_AndUnstageAsync_MoveFilesBetweenStagedAndUnstaged()
    {
        string clone = await CloneAsync();
        TempRepositories.WriteFile(clone, "new.txt", "new");
        TempRepositories.WriteFile(clone, "README.md", "changed");

        await _tree.StageAsync(clone, ["new.txt"], TestContext.Current.CancellationToken);

        FileChange added = Assert.Single(_reader.ReadStatus(clone).Changes, c => c.Path == "new.txt");
        Assert.Equal(FileChangeKind.Added, added.Staged);

        await _tree.UnstageAsync(clone, ["new.txt"], TestContext.Current.CancellationToken);

        FileChange untracked = Assert.Single(_reader.ReadStatus(clone).Changes, c => c.Path == "new.txt");
        Assert.True(untracked.IsUntracked);
        Assert.False(untracked.IsStaged);
    }

    [Fact]
    public async Task StageAsync_DeletedFile_StagesTheDeletion()
    {
        string clone = await CloneAsync();
        File.Delete(Path.Combine(clone, "README.md"));

        await _tree.StageAsync(clone, ["README.md"], TestContext.Current.CancellationToken);

        Assert.Equal(FileChangeKind.Deleted, Assert.Single(_reader.ReadStatus(clone).Changes).Staged);
    }

    [Fact]
    public async Task CommitAsync_CommitsStagedFilesWithSubjectAndBody()
    {
        string clone = await CloneAsync();
        TempRepositories.WriteFile(clone, "a.txt", "a");
        TempRepositories.WriteFile(clone, "b.txt", "b");
        await _tree.StageAsync(clone, ["a.txt"], TestContext.Current.CancellationToken);

        await _tree.CommitAsync(clone, "Add a\n\nBecause it's needed.", TestContext.Current.CancellationToken);

        CommitInfo head = _reader.ReadHistory(clone, new HistoryOptions { MaxCount = 1 })[0];
        Assert.Equal("Add a", head.Subject);
        Assert.Contains("Because it's needed.", head.Message);
        Assert.Equal(["a.txt"], _reader.ReadCommitChanges(clone, head.Sha).Select(c => c.Path));
        Assert.Equal("b.txt", Assert.Single(_reader.ReadStatus(clone).Changes).Path);
    }

    [Fact]
    public async Task CommitAsync_NothingStaged_Throws()
    {
        string clone = await CloneAsync();

        await Assert.ThrowsAsync<GitCommandException>(
            () => _tree.CommitAsync(clone, "Nothing", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WriteMessageAsync_RoundTripsAndIsClearedByACommit()
    {
        string clone = await CloneAsync();
        TempRepositories.WriteFile(clone, "a.txt", "a");
        await _tree.StageAsync(clone, ["a.txt"], TestContext.Current.CancellationToken);

        await _tree.WriteMessageAsync(clone, "Draft\n\nBody", TestContext.Current.CancellationToken);
        Assert.Equal("Draft\n\nBody", await _tree.ReadMessageAsync(clone, TestContext.Current.CancellationToken));

        await _tree.CommitAsync(clone, "Draft\n\nBody", TestContext.Current.CancellationToken);
        Assert.Equal("", await _tree.ReadMessageAsync(clone, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CommitAsync_InSubmodule_CommitsThereAndChangesThePointerInTheParent()
    {
        string remote = await _repos.CreateRemoteWithSubmoduleAsync();
        string app = await _repos.CloneAsync(remote, "app", "--recurse-submodules");
        string lib = Path.Combine(app, "lib");
        TempRepositories.WriteFile(lib, "x.txt", "x");

        await _tree.StageAsync(lib, ["x.txt"], TestContext.Current.CancellationToken);
        await _tree.WriteMessageAsync(lib, "In lib", TestContext.Current.CancellationToken);
        await _tree.CommitAsync(lib, "In lib", TestContext.Current.CancellationToken);

        Assert.Equal("In lib", _reader.ReadHistory(lib, new HistoryOptions { MaxCount = 1 })[0].Subject);
        Assert.NotEqual(SubmoduleCommitChange.None, Assert.Single(_reader.ReadStatus(app).Submodules).CommitChange);
    }

    [Fact]
    public async Task DiscardAsync_RestoresTrackedFilesAndDeletesUntrackedOnes_KeepingStagedChanges()
    {
        string clone = await CloneAsync();
        TempRepositories.WriteFile(clone, "README.md", "changed");
        TempRepositories.WriteFile(clone, "new.txt", "new");
        TempRepositories.WriteFile(clone, "staged.txt", "staged");
        await _tree.StageAsync(clone, ["staged.txt"], TestContext.Current.CancellationToken);

        await _tree.DiscardAsync(clone, ["README.md"], ["new.txt"], TestContext.Current.CancellationToken);

        Assert.NotEqual("changed", File.ReadAllText(Path.Combine(clone, "README.md")));
        Assert.False(File.Exists(Path.Combine(clone, "new.txt")));
        FileChange left = Assert.Single(_reader.ReadStatus(clone).Changes);
        Assert.Equal("staged.txt", left.Path);
        Assert.Equal(FileChangeKind.Added, left.Staged);
    }

    [Fact]
    public async Task DiscardAllAsync_RemovesEveryUnstagedChange()
    {
        string clone = await CloneAsync();
        TempRepositories.WriteFile(clone, "README.md", "changed");
        TempRepositories.WriteFile(clone, "sub/new.txt", "new");

        await _tree.DiscardAllAsync(clone, TestContext.Current.CancellationToken);

        Assert.Empty(_reader.ReadStatus(clone).Changes);
        Assert.False(Directory.Exists(Path.Combine(clone, "sub")));
    }

    [Theory]
    [InlineData(GitResetMode.Soft, true, true)]
    [InlineData(GitResetMode.Mixed, false, true)]
    [InlineData(GitResetMode.Hard, false, false)]
    public async Task ResetAsync_MovesTheBranchAndResetsAsTheModeSays(GitResetMode mode, bool staged, bool fileKept)
    {
        string clone = await CloneAsync();
        string first = _reader.ReadHistory(clone, new HistoryOptions { MaxCount = 1 })[0].Sha;
        await _repos.CommitFileAsync(clone, "later.txt", "later", "Later");

        await _tree.ResetAsync(clone, first, mode, TestContext.Current.CancellationToken);

        Assert.Equal(first, _reader.ReadHistory(clone, new HistoryOptions { MaxCount = 1 })[0].Sha);
        Assert.Equal(fileKept, File.Exists(Path.Combine(clone, "later.txt")));
        FileChange[] changes = [.. _reader.ReadStatus(clone).Changes];
        Assert.Equal(staged, changes.Any(change => change.IsStaged));
        Assert.Equal(fileKept, changes.Length == 1);
    }

    private async Task<string> CloneAsync()
    {
        string remote = await _repos.CreateRemoteAsync("repo");
        return await _repos.CloneAsync(remote, "clone");
    }
}
