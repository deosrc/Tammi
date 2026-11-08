using System.ComponentModel;
using Deosrc.Tammi.Core.Agents;
using Deosrc.Tammi.Core.Platform;

namespace Deosrc.Tammi.Core.Tests.Agents;

public class SupportedAgentCheckerTests
{
    private static readonly SupportedAgent Agent =
        new(new AgentDefinition("Test Agent", "test-agent"), ["--version"], "https://example.com/install");

    [Fact]
    public async Task CheckAsync_WhenCliRuns_ReportsPathAndVersion()
    {
        var launcher = new FakeProcessLauncher { Result = new ProcessResult(0, "\n2.1.0 (Test Agent)\r\nmore\n", "") };

        var status = await new SupportedAgentChecker(launcher).CheckAsync(Agent, TestContext.Current.CancellationToken);

        Assert.True(status.IsAvailable);
        Assert.Null(status.Problem);
        Assert.Equal("/bin/test-agent", status.ExecutablePath);
        Assert.Equal("2.1.0 (Test Agent)", status.Version);
        var request = Assert.Single(launcher.Requests);
        Assert.Equal("/bin/test-agent", request.FileName);
        Assert.Equal(["--version"], request.Arguments);
    }

    [Fact]
    public async Task CheckAsync_WhenCliIsMissing_SaysSoWithoutRunningAnything()
    {
        var launcher = new FakeProcessLauncher { Path = null };

        var status = await new SupportedAgentChecker(launcher).CheckAsync(Agent, TestContext.Current.CancellationToken);

        Assert.False(status.IsAvailable);
        Assert.Null(status.ExecutablePath);
        Assert.Contains("Couldn't find 'test-agent'", status.Problem);
        Assert.Contains("Install Test Agent", status.Problem);
        Assert.Empty(launcher.Requests);
    }

    [Fact]
    public async Task CheckAsync_WhenCliFails_ReportsExitCodeAndError()
    {
        var launcher = new FakeProcessLauncher { Result = new ProcessResult(2, "", "\nError: broken install\n") };

        var status = await new SupportedAgentChecker(launcher).CheckAsync(Agent, TestContext.Current.CancellationToken);

        Assert.False(status.IsAvailable);
        Assert.Equal("/bin/test-agent", status.ExecutablePath);
        Assert.Equal("'test-agent --version' failed with exit code 2: Error: broken install", status.Problem);
    }

    [Fact]
    public async Task CheckAsync_WhenCliCantStart_ReportsWhy()
    {
        var launcher = new FakeProcessLauncher { Exception = new Win32Exception("Permission denied") };

        var status = await new SupportedAgentChecker(launcher).CheckAsync(Agent, TestContext.Current.CancellationToken);

        Assert.False(status.IsAvailable);
        Assert.Equal("Couldn't run '/bin/test-agent': Permission denied", status.Problem);
    }

    [Fact]
    public async Task CheckAsync_WhenCliDoesNotFinish_SaysItTimedOut()
    {
        var launcher = new FakeProcessLauncher { Hangs = true };

        var status = await new SupportedAgentChecker(launcher, TimeSpan.FromMilliseconds(50))
            .CheckAsync(Agent, TestContext.Current.CancellationToken);

        Assert.False(status.IsAvailable);
        Assert.Equal("'test-agent --version' didn't finish within 0.1 seconds.", status.Problem);
    }

    [Fact]
    public async Task CheckAsync_WhenCancelled_Throws()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var launcher = new FakeProcessLauncher();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new SupportedAgentChecker(launcher).CheckAsync(Agent, cancellation.Token));
    }

    [Fact]
    public async Task CheckAsync_WithRealProcess_ReportsItsVersion()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("This test runs a real shell script.");
            return;
        }

        var agent = new SupportedAgent(new AgentDefinition("Shell", "/bin/sh"), ["-c", "echo 1.2.3"], "https://example.com");

        var status = await new SupportedAgentChecker(new TestProcessLauncher())
            .CheckAsync(agent, TestContext.Current.CancellationToken);

        Assert.True(status.IsAvailable, status.Problem);
        Assert.Equal("1.2.3", status.Version);
    }

    [Fact]
    public void ClaudeCode_ChecksClaudeVersion()
    {
        Assert.Same(AgentDefinition.ClaudeCode, SupportedAgent.ClaudeCode.Definition);
        Assert.Equal(["--version"], SupportedAgent.ClaudeCode.VersionArguments);
        Assert.Contains(SupportedAgent.ClaudeCode, SupportedAgent.All);
    }

    private sealed class FakeProcessLauncher : IProcessLauncher
    {
        public string? Path { get; init; } = "/bin/test-agent";

        public ProcessResult Result { get; init; } = new(0, "1.0.0", "");

        public Exception? Exception { get; init; }

        public bool Hangs { get; init; }

        public List<ProcessRequest> Requests { get; } = [];

        public string? FindExecutable(string name) => Path;

        public async Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            if (Hangs)
            {
                await Task.Delay(System.Threading.Timeout.Infinite, cancellationToken);
            }

            return await ( Exception is null ? Task.FromResult(Result) : Task.FromException<ProcessResult>(Exception));
        }

        public int Start(ProcessRequest request) => throw new NotSupportedException();

        public void StartDetached(ProcessRequest request) => throw new NotSupportedException();

        public void Open(string target) => throw new NotSupportedException();
    }
}
