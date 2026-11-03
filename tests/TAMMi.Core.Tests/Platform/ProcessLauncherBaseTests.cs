using TAMMi.Core.Platform;

namespace TAMMi.Core.Tests.Platform;

/// <summary>
/// Runs real processes. They use <c>git</c>, which TAMMi requires.
/// </summary>
public class ProcessLauncherBaseTests
{
    private readonly TestProcessLauncher _launcher = new();

    [Fact]
    public async Task RunAsync_CapturesOutputAndExitCode()
    {
        var result = await _launcher.RunAsync(
            new ProcessRequest("git", "--version"),
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.StartsWith("git version", result.StandardOutput);
    }

    [Fact]
    public async Task RunAsync_ReportsFailure()
    {
        var result = await _launcher.RunAsync(
            new ProcessRequest("git", "not-a-git-command"),
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Contains("not-a-git-command", result.StandardError);
    }

    [Fact]
    public async Task RunAsync_UsesWorkingDirectoryAndEnvironment()
    {
        var directory = Directory.CreateTempSubdirectory("tammi-tests-");
        try
        {
            var request = new ProcessRequest("git", "rev-parse", "--is-inside-work-tree")
            {
                WorkingDirectory = directory.FullName,
                Environment = new Dictionary<string, string?> { ["GIT_CEILING_DIRECTORIES"] = directory.FullName },
            };

            var result = await _launcher.RunAsync(request, TestContext.Current.CancellationToken);

            Assert.False(result.Succeeded);
            Assert.Contains("not a git repository", result.StandardError);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_Throws_WhenExecutableIsMissing()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _launcher.RunAsync(new ProcessRequest("tammi-no-such-tool"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RunAsync_Throws_WhenCancelled()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _launcher.RunAsync(new ProcessRequest("git", "--version"), cancellation.Token));
    }
}
