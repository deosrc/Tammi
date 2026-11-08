using Deosrc.Tammi.Core.Git;

namespace Deosrc.Tammi.Core.Tests.Git;

public sealed class GitRepositoryReaderRefsTests : IDisposable
{
    private readonly TempRepositories _repos = new();
    private readonly GitRepositoryReader _reader = new();

    public void Dispose() => _repos.Dispose();

    [Fact]
    public async Task ReadRefs_ReturnsBranchesThenRemoteBranchesThenTags()
    {
        string clone = await CloneAsync();
        string initial = await HeadAsync(clone);
        await _repos.RunAsync(clone, "tag", "lightweight");
        await _repos.CommitFileAsync(clone, "a.txt", "a", "Add a");
        string head = await HeadAsync(clone);
        await _repos.RunAsync(clone, "tag", "-a", "annotated", "-m", "An annotated tag");
        await _repos.RunAsync(clone, "branch", "feature", initial);

        GitRefs refs = _reader.ReadRefs(clone);

        Assert.Equal(head, refs.HeadSha);
        Assert.Equal("main", refs.HeadBranch);
        Assert.Equal(
            [
                new GitReference("feature", GitReferenceKind.LocalBranch, initial),
                new GitReference("main", GitReferenceKind.LocalBranch, head),
                new GitReference("origin/main", GitReferenceKind.RemoteBranch, initial),
                new GitReference("annotated", GitReferenceKind.Tag, head),
                new GitReference("lightweight", GitReferenceKind.Tag, initial),
            ],
            refs.References);
    }

    [Fact]
    public async Task ReadRefs_DetachedHead_HasNoHeadBranch()
    {
        string clone = await CloneAsync();
        string head = await HeadAsync(clone);
        await _repos.RunAsync(clone, "switch", "--detach");

        GitRefs refs = _reader.ReadRefs(clone);

        Assert.Equal(head, refs.HeadSha);
        Assert.Null(refs.HeadBranch);
    }

    [Fact]
    public async Task ReadRefs_EmptyRepository_IsEmpty()
    {
        string repo = _repos.PathTo("empty");
        await _repos.RunAsync(_repos.Root, "init", repo);

        Assert.Equal(GitRefs.Empty, _reader.ReadRefs(repo));
    }

    [Fact]
    public async Task ReadRefs_SameRefs_AreEqual()
    {
        string clone = await CloneAsync();

        GitRefs before = _reader.ReadRefs(clone);
        Assert.Equal(before, _reader.ReadRefs(clone));

        await _repos.CommitFileAsync(clone, "a.txt", "a", "Add a");
        Assert.NotEqual(before, _reader.ReadRefs(clone));
    }

    private async Task<string> HeadAsync(string repository) =>
        Assert.Single((await _repos.RunAsync(repository, "rev-parse", "HEAD")).OutputLines);

    private async Task<string> CloneAsync()
    {
        string remote = await _repos.CreateRemoteAsync("repo");
        return await _repos.CloneAsync(remote, "clone");
    }
}
