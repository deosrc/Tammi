using Deosrc.Tammi.Core.Git;

namespace Deosrc.Tammi.Core.Tests.Git;

public sealed class GitRepositoryReaderDiffTests : IDisposable
{
    private readonly TempRepositories _repos = new();
    private readonly GitRepositoryReader _reader = new();

    public void Dispose() => _repos.Dispose();

    [Fact]
    public async Task ReadDiff_Unstaged_ShowsAddedAndRemovedLinesWithNumbers()
    {
        string clone = await CloneAsync();
        await _repos.CommitFileAsync(clone, "f.txt", "one\ntwo\nthree\n", "Add f");
        TempRepositories.WriteFile(clone, "f.txt", "one\nTWO\nthree\n");

        FileDiff diff = _reader.ReadDiff(clone, GitDiffSource.Unstaged, "f.txt");

        DiffHunk hunk = Assert.Single(diff.Hunks);
        Assert.Contains(hunk.Lines, l => l is { Kind: DiffLineKind.Removed, Text: "two", OldNumber: 2, NewNumber: null });
        Assert.Contains(hunk.Lines, l => l is { Kind: DiffLineKind.Added, Text: "TWO", OldNumber: null, NewNumber: 2 });
        Assert.Contains(hunk.Lines, l => l is { Kind: DiffLineKind.Context, Text: "three", OldNumber: 3, NewNumber: 3 });
    }

    [Fact]
    public async Task ReadDiff_UntrackedFile_ShowsAllLinesAdded()
    {
        string clone = await CloneAsync();
        TempRepositories.WriteFile(clone, "new.txt", "a\nb\n");

        FileDiff diff = _reader.ReadDiff(clone, GitDiffSource.Unstaged, "new.txt");

        Assert.All(Assert.Single(diff.Hunks).Lines, l => Assert.Equal(DiffLineKind.Added, l.Kind));
    }

    [Fact]
    public async Task ReadDiff_Staged_ComparesTheIndexWithHead()
    {
        string clone = await CloneAsync();
        await _repos.CommitFileAsync(clone, "f.txt", "one\n", "Add f");
        TempRepositories.WriteFile(clone, "f.txt", "two\n");
        await _repos.RunAsync(clone, "add", "f.txt");
        TempRepositories.WriteFile(clone, "f.txt", "three\n");

        FileDiff diff = _reader.ReadDiff(clone, GitDiffSource.Staged, "f.txt");

        Assert.Contains(diff.Hunks[0].Lines, l => l is { Kind: DiffLineKind.Added, Text: "two" });
    }

    [Fact]
    public async Task ReadDiff_Commit_ComparesWithTheFirstParent()
    {
        string clone = await CloneAsync();
        await _repos.CommitFileAsync(clone, "f.txt", "one\n", "Add f");
        await _repos.CommitFileAsync(clone, "f.txt", "one\ntwo\n", "Change f");
        string sha = _reader.ReadHistory(clone, new HistoryOptions { MaxCount = 1 })[0].Sha;

        FileDiff diff = _reader.ReadDiff(clone, GitDiffSource.Commit, "f.txt", sha: sha);

        DiffLine added = Assert.Single(diff.Hunks[0].Lines, l => l.Kind == DiffLineKind.Added);
        Assert.Equal("two", added.Text);
    }

    [Fact]
    public async Task ReadDiff_BinaryFile_IsFlaggedAndHasNoHunks()
    {
        string clone = await CloneAsync();
        File.WriteAllBytes(Path.Combine(clone, "b.bin"), [0, 1, 2, 0, 3]);
        await _repos.RunAsync(clone, "add", "b.bin");

        FileDiff diff = _reader.ReadDiff(clone, GitDiffSource.Staged, "b.bin");

        Assert.True(diff.IsBinary);
        Assert.Empty(diff.Hunks);
    }

    [Fact]
    public async Task ReadDiff_UnchangedFile_HasNoHunks()
    {
        string clone = await CloneAsync();

        Assert.Empty(_reader.ReadDiff(clone, GitDiffSource.Unstaged, "README.md").Hunks);
    }

    private async Task<string> CloneAsync()
    {
        string remote = await _repos.CreateRemoteAsync("repo");
        return await _repos.CloneAsync(remote, "clone");
    }
}
