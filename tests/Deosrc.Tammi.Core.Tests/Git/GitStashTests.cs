using Deosrc.Tammi.Core.Git;

namespace Deosrc.Tammi.Core.Tests.Git;

public sealed class GitStashTests : IDisposable
{
    private readonly TempRepositories _repos = new();
    private readonly GitStash _stash;
    private readonly GitRepositoryReader _reader = new();

    public GitStashTests() => _stash = new GitStash(_repos.Git);

    public void Dispose() => _repos.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<string> CloneAsync()
    {
        string remote = await _repos.CreateRemoteAsync("origin");
        return await _repos.CloneAsync(remote, "mine");
    }

    private IEnumerable<string> ChangedPaths(string clone) => _reader.ReadStatus(clone).Changes.Select(c => c.Path).Order();

    [Fact]
    public async Task PushAsync_NoFiles_StashesEverythingIncludingNewFiles()
    {
        string clone = await CloneAsync();
        TempRepositories.WriteFile(clone, "README.md", "changed");
        TempRepositories.WriteFile(clone, "new.txt", "new");

        Assert.True(await _stash.PushAsync(clone, null, "Try it", Token));

        Assert.Empty(ChangedPaths(clone));
        GitStashEntry entry = Assert.Single(await _stash.ListAsync(clone, Token));
        Assert.Equal(0, entry.Index);
        Assert.Equal("stash@{0}", entry.Name);
        Assert.EndsWith("Try it", entry.Message);
    }

    [Fact]
    public async Task PushAsync_Files_StashesOnlyThose()
    {
        string clone = await CloneAsync();
        TempRepositories.WriteFile(clone, "README.md", "changed");
        TempRepositories.WriteFile(clone, "new.txt", "new");

        await _stash.PushAsync(clone, ["new.txt"], null, Token);

        Assert.Equal(["README.md"], ChangedPaths(clone));
        Assert.Single(await _stash.ListAsync(clone, Token));
    }

    [Fact]
    public async Task PushAsync_NothingToStash_ReturnsFalse()
    {
        string clone = await CloneAsync();

        Assert.False(await _stash.PushAsync(clone, null, null, Token));

        Assert.Empty(await _stash.ListAsync(clone, Token));
    }

    [Fact]
    public async Task ApplyAsync_RestoresTheChangesAndKeepsTheStash()
    {
        string clone = await CloneAsync();
        TempRepositories.WriteFile(clone, "README.md", "changed");
        await _stash.PushAsync(clone, null, null, Token);

        await _stash.ApplyAsync(clone, 0, Token);

        Assert.Equal(["README.md"], ChangedPaths(clone));
        Assert.Single(await _stash.ListAsync(clone, Token));
    }

    [Fact]
    public async Task PopAsync_RestoresTheChangesAndDropsTheStash()
    {
        string clone = await CloneAsync();
        TempRepositories.WriteFile(clone, "README.md", "changed");
        await _stash.PushAsync(clone, null, null, Token);

        await _stash.PopAsync(clone, 0, Token);

        Assert.Equal(["README.md"], ChangedPaths(clone));
        Assert.Empty(await _stash.ListAsync(clone, Token));
    }

    [Fact]
    public async Task PopAsync_Conflict_ThrowsAndKeepsTheStash()
    {
        string clone = await CloneAsync();
        TempRepositories.WriteFile(clone, "README.md", "stashed");
        await _stash.PushAsync(clone, null, null, Token);
        TempRepositories.WriteFile(clone, "README.md", "different");

        await Assert.ThrowsAsync<GitCommandException>(() => _stash.PopAsync(clone, 0, Token));

        Assert.Single(await _stash.ListAsync(clone, Token));
    }

    [Fact]
    public async Task DropAsync_DeletesTheChosenStash()
    {
        string clone = await CloneAsync();
        TempRepositories.WriteFile(clone, "README.md", "one");
        await _stash.PushAsync(clone, null, "first", Token);
        TempRepositories.WriteFile(clone, "README.md", "two");
        await _stash.PushAsync(clone, null, "second", Token);

        await _stash.DropAsync(clone, 0, Token);

        GitStashEntry left = Assert.Single(await _stash.ListAsync(clone, Token));
        Assert.EndsWith("first", left.Message);
    }
}
