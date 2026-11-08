using Deosrc.Tammi.Core.Git;

namespace Deosrc.Tammi.Core.Tests.Git;

public sealed class GitCliTests : IDisposable
{
    private readonly TempRepositories _repos = new();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public void Dispose() => _repos.Dispose();

    [Fact]
    public async Task DetectAsync_GitInstalled_ReturnsVersion()
    {
        GitInstallation? installation = await _repos.Git.DetectAsync(CancellationToken);

        Assert.NotNull(installation);
        Assert.Equal("git", installation.ExecutablePath);
        Assert.True(installation.Version >= new Version(2, 0));
        Assert.StartsWith(installation.Version.ToString(), installation.VersionText);
    }

    [Fact]
    public void Redact_HidesUserInfoInUrls_AndKeepsTheRest()
    {
        Assert.Equal(
            "clone https://***@github.com/acme/api.git ssh://***@example.com/repo api",
            GitCli.Redact([
                "clone", "https://user:token@github.com/acme/api.git", "ssh://git@example.com/repo", "api",
            ]));
    }

    [Fact]
    public async Task DetectAsync_GitMissing_ReturnsNull()
    {
        var git = new GitCli(new GitCliOptions { ExecutablePath = "tammi-git-does-not-exist" });

        Assert.Null(await git.DetectAsync(CancellationToken));
    }

    [Fact]
    public async Task RunAsync_GitMissing_ThrowsClearError()
    {
        var git = new GitCli(new GitCliOptions { ExecutablePath = "tammi-git-does-not-exist" });

        var ex = await Assert.ThrowsAsync<GitNotInstalledException>(
            () => git.RunAsync(_repos.Root, ["--version"], cancellationToken: CancellationToken));

        Assert.Equal("tammi-git-does-not-exist", ex.ExecutablePath);
        Assert.Contains("Git isn't installed", ex.Message);
    }

    [Fact]
    public async Task RunAsync_Succeeds_ReturnsOutput()
    {
        string remote = await _repos.CreateRemoteAsync("repo");
        string clone = await _repos.CloneAsync(remote, "clone");

        GitCommandResult result = await _repos.RunAsync(clone, "rev-parse", "--abbrev-ref", "HEAD");

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(["rev-parse", "--abbrev-ref", "HEAD"], result.Arguments);
        Assert.Equal(clone, result.WorkingDirectory);
        Assert.Equal(["main"], result.OutputLines);
    }

    [Fact]
    public async Task RunAsync_Fails_ThrowsWithStructuredResult()
    {
        var ex = await Assert.ThrowsAsync<GitCommandException>(
            () => _repos.RunAsync(_repos.Root, "rev-parse", "HEAD"));

        Assert.NotEqual(0, ex.Result.ExitCode);
        Assert.Contains("not a git repository", ex.Result.StandardError);
        Assert.Contains("git rev-parse HEAD", ex.Message);
        Assert.Contains("not a git repository", ex.Message);
    }

    [Fact]
    public async Task RunAsync_FailsWithoutThrowOnError_ReturnsResult()
    {
        GitCommandResult result = await _repos.Git.RunAsync(
            _repos.Root,
            ["rev-parse", "HEAD"],
            new GitCommandOptions { ThrowOnError = false },
            CancellationToken);

        Assert.False(result.Succeeded);
        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public async Task RunAsync_NullSeparatedOutput_SplitsIntoUnquotedFields()
    {
        string remote = await _repos.CreateRemoteAsync("repo");
        string clone = await _repos.CloneAsync(remote, "clone");
        TempRepositories.WriteFile(clone, "with space.txt", "a");
        TempRepositories.WriteFile(clone, "ünïcode.txt", "b");

        GitCommandResult result = await _repos.RunAsync(clone, "status", "--porcelain=v2", "-z");

        Assert.Equal(["? with space.txt", "? ünïcode.txt"], result.OutputFields);
    }

    [Fact]
    public async Task RunAsync_StandardInput_IsPassedToGit()
    {
        GitCommandResult result = await _repos.Git.RunAsync(
            _repos.Root,
            ["hash-object", "--stdin"],
            new GitCommandOptions { StandardInput = "hello\n" },
            CancellationToken);

        Assert.Equal(["ce013625030ba8dba906f756967f9e9ca394464a"], result.OutputLines);
    }

    [Fact]
    public async Task RunAsync_Cancelled_Throws()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _repos.Git.RunAsync(_repos.Root, ["--version"], cancellationToken: cts.Token));
    }
}
