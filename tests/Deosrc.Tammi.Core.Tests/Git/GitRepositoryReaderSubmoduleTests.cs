using TAMMi.Core.Git;

namespace TAMMi.Core.Tests.Git;

public sealed class GitRepositoryReaderSubmoduleTests : IDisposable
{
    private readonly TempRepositories _repos = new();
    private readonly GitRepositoryReader _reader = new();

    public void Dispose() => _repos.Dispose();

    [Fact]
    public async Task CleanSubmodule_IsNotChanged()
    {
        string app = await CloneAppWithSubmoduleAsync();

        RepositoryStatus status = _reader.ReadStatus(app);

        SubmoduleStatus submodule = Assert.Single(status.Submodules);
        Assert.Equal("lib", submodule.Name);
        Assert.Equal("lib", submodule.Path);
        Assert.Equal(SubmoduleCommitChange.None, submodule.CommitChange);
        Assert.True(submodule.IsInitialized);
        Assert.NotNull(submodule.Status);
        Assert.Null(submodule.Status.Branch);
        Assert.Equal(Path.Combine(app, "lib"), submodule.Status.Path);
        Assert.False(submodule.Status.IsChanged);
        Assert.Empty(status.Changes);
        Assert.False(status.IsChanged);
    }

    [Fact]
    public async Task UninitialisedSubmodule_IsNotChanged()
    {
        string appRemote = await CreateAppRemoteWithSubmoduleAsync();
        string app = await _repos.CloneAsync(appRemote, "app");

        RepositoryStatus status = _reader.ReadStatus(app);

        SubmoduleStatus submodule = Assert.Single(status.Submodules);
        Assert.False(submodule.IsInitialized);
        Assert.Null(submodule.Status);
        Assert.Equal(SubmoduleCommitChange.None, submodule.CommitChange);
        Assert.False(status.IsChanged);
    }

    [Fact]
    public async Task UncommittedChangeInSubmodule_ParentIsChanged()
    {
        string app = await CloneAppWithSubmoduleAsync();
        TempRepositories.WriteFile(Path.Combine(app, "lib"), "README.md", "changed in submodule\n");

        RepositoryStatus status = _reader.ReadStatus(app);

        SubmoduleStatus submodule = Assert.Single(status.Submodules);
        Assert.Equal(SubmoduleCommitChange.None, submodule.CommitChange);
        Assert.Equal(
            [new FileChange("README.md", FileChangeKind.None, FileChangeKind.Modified)],
            submodule.Status!.Changes);
        Assert.True(submodule.Status.IsChanged);

        // The change belongs to the submodule, so the parent has no uncommitted changes of its own.
        Assert.Empty(status.Changes);
        Assert.False(status.HasUncommittedChanges);
        Assert.True(status.IsChanged);
    }

    [Fact]
    public async Task UntrackedFileInSubmodule_ParentIsChanged()
    {
        string app = await CloneAppWithSubmoduleAsync();
        TempRepositories.WriteFile(Path.Combine(app, "lib"), "new.txt", "new");

        RepositoryStatus status = _reader.ReadStatus(app);

        SubmoduleStatus submodule = Assert.Single(status.Submodules);
        Assert.True(Assert.Single(submodule.Status!.Changes).IsUntracked);
        Assert.True(status.IsChanged);
    }

    [Fact]
    public async Task UnpushedCommitInSubmodule_IsUnstagedCommitChangeAndUnpushed()
    {
        string app = await CloneAppWithSubmoduleAsync();
        string lib = Path.Combine(app, "lib");
        await _repos.CommitFileAsync(lib, "new.txt", "new", "Submodule commit");

        RepositoryStatus status = _reader.ReadStatus(app);

        SubmoduleStatus submodule = Assert.Single(status.Submodules);
        Assert.Equal(SubmoduleCommitChange.Unstaged, submodule.CommitChange);
        Assert.Equal(1, submodule.Status!.UnpushedCommitCount);
        Assert.True(submodule.Status.IsChanged);
        Assert.Empty(status.Changes);
        Assert.True(status.HasUncommittedChanges);
        Assert.True(status.IsChanged);
    }

    [Fact]
    public async Task StagedSubmoduleCommit_IsStagedCommitChange()
    {
        string app = await CloneAppWithSubmoduleAsync();
        string lib = Path.Combine(app, "lib");
        await _repos.CommitFileAsync(lib, "new.txt", "new", "Submodule commit");
        await _repos.RunAsync(lib, "push", "origin", "HEAD:main");
        await _repos.RunAsync(app, "add", "lib");

        RepositoryStatus status = _reader.ReadStatus(app);

        SubmoduleStatus submodule = Assert.Single(status.Submodules);
        Assert.Equal(SubmoduleCommitChange.Staged, submodule.CommitChange);
        Assert.Equal(0, submodule.Status!.UnpushedCommitCount);
        Assert.False(submodule.Status.IsChanged);
        Assert.True(status.HasUncommittedChanges);
        Assert.True(status.IsChanged);
    }

    [Fact]
    public async Task NestedSubmodule_ChangesPropagateToTopLevel()
    {
        string appRemote = await _repos.CreateRemoteWithSubmoduleAsync(nested: true);
        string app = await _repos.CloneAsync(appRemote, "app", "--recurse-submodules");
        TempRepositories.WriteFile(Path.Combine(app, "lib", "core"), "new.txt", "new");

        RepositoryStatus status = _reader.ReadStatus(app);

        SubmoduleStatus lib = Assert.Single(status.Submodules);
        SubmoduleStatus core = Assert.Single(lib.Status!.Submodules);
        Assert.True(core.Status!.IsChanged);
        Assert.True(lib.Status.IsChanged);
        Assert.True(status.IsChanged);
    }

    private Task<string> CreateAppRemoteWithSubmoduleAsync() => _repos.CreateRemoteWithSubmoduleAsync();

    private async Task<string> CloneAppWithSubmoduleAsync()
    {
        string appRemote = await CreateAppRemoteWithSubmoduleAsync();
        return await _repos.CloneAsync(appRemote, "app", "--recurse-submodules");
    }
}
