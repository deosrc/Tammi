using Deosrc.Tammi.Core.Git;

namespace Deosrc.Tammi.Core.Tests.Git;

public sealed class GitRepositoryReaderCommitChangesTests : IDisposable
{
    private readonly TempRepositories _repos = new();
    private readonly GitRepositoryReader _reader = new();

    public void Dispose() => _repos.Dispose();

    [Fact]
    public async Task ReadCommitChanges_ReturnsChangesAgainstTheFirstParent()
    {
        string clone = await CloneAsync();
        TempRepositories.WriteFile(clone, "to-delete.txt", "delete me");
        TempRepositories.WriteFile(clone, "to-rename.txt", "a file with enough content to be found as a rename\n");
        await _repos.RunAsync(clone, "add", "--all");
        await _repos.RunAsync(clone, "commit", "-m", "Add files");

        TempRepositories.WriteFile(clone, "README.md", "# changed\n");
        TempRepositories.WriteFile(clone, "added.txt", "new");
        File.Delete(Path.Combine(clone, "to-delete.txt"));
        await _repos.RunAsync(clone, "mv", "to-rename.txt", "renamed.txt");
        await _repos.RunAsync(clone, "add", "--all");
        await _repos.RunAsync(clone, "commit", "-m", "Change files");

        IReadOnlyList<CommitFileChange> changes = _reader.ReadCommitChanges(clone, await HeadAsync(clone));

        Assert.Equal(
            [
                new CommitFileChange("README.md", FileChangeKind.Modified),
                new CommitFileChange("added.txt", FileChangeKind.Added),
                new CommitFileChange("renamed.txt", FileChangeKind.Renamed, "to-rename.txt"),
                new CommitFileChange("to-delete.txt", FileChangeKind.Deleted),
            ],
            changes);
    }

    [Fact]
    public async Task ReadCommitChanges_RootCommit_ListsEveryFileAsAdded()
    {
        string clone = await CloneAsync();

        IReadOnlyList<CommitFileChange> changes = _reader.ReadCommitChanges(clone, await HeadAsync(clone));

        Assert.Equal([new CommitFileChange("README.md", FileChangeKind.Added)], changes);
    }

    [Fact]
    public async Task ReadCommitChanges_UnknownCommit_Throws()
    {
        string clone = await CloneAsync();

        Assert.Throws<GitException>(() => _reader.ReadCommitChanges(clone, new string('0', 40)));
    }

    private async Task<string> HeadAsync(string repository) =>
        Assert.Single((await _repos.RunAsync(repository, "rev-parse", "HEAD")).OutputLines);

    private async Task<string> CloneAsync()
    {
        string remote = await _repos.CreateRemoteAsync("repo");
        return await _repos.CloneAsync(remote, "clone");
    }
}
