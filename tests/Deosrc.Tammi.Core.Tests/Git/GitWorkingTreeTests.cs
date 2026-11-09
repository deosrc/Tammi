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

    private async Task<string> CloneAsync()
    {
        string remote = await _repos.CreateRemoteAsync("repo");
        return await _repos.CloneAsync(remote, "clone");
    }
}
