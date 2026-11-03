using TAMMi.Core.Agents;
using TAMMi.Core.Platform;
using TAMMi.Core.Platform.Pty;
using TAMMi.Core.Tests.Platform.Pty;

namespace TAMMi.Core.Tests.Agents;

public class AgentLauncherTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    [Fact]
    public void Start_StartsDefinitionsCommandInWorkingDirectory()
    {
        var pty = new RecordingPtyService();
        var definition = new AgentDefinition("Test", "test-agent", "--flag", "value with spaces");
        var directory = Path.GetTempPath();

        var agent = new AgentLauncher(pty).Start(definition, directory, new PtySize(100, 30));

        var request = Assert.Single(pty.Requests);
        Assert.Equal("test-agent", request.Request.FileName);
        Assert.Equal(["--flag", "value with spaces"], request.Request.Arguments);
        Assert.Equal(Path.GetFullPath(directory), request.Request.WorkingDirectory);
        Assert.Equal(new PtySize(100, 30), request.Size);
        Assert.Same(definition, agent.Definition);
        Assert.Equal(Path.GetFullPath(directory), agent.WorkingDirectory);
        Assert.Equal(AgentState.Running, agent.State);
    }

    [Fact]
    public void Start_WithInitialPrompt_PassesItAsDefinitionSays()
    {
        var pty = new RecordingPtyService();
        var definition = new AgentDefinition("Test", "test-agent", "--flag") { PromptArguments = ["-p", "{prompt}"] };
        var prompt = InitialPrompt.Expand(InitialPrompt.DefaultTemplate, "/ws/.tammi/tasks.md");

        new AgentLauncher(pty).Start(definition, Path.GetTempPath(), PtySize.Default, prompt);

        Assert.Equal(["--flag", "-p", prompt], Assert.Single(pty.Requests).Request.Arguments);
    }

    [Fact]
    public void Start_WithNullCharacterInPrompt_Throws()
    {
        var pty = new RecordingPtyService();

        Assert.Throws<ArgumentException>(() => new AgentLauncher(pty).Start(
            AgentDefinition.ClaudeCode, Path.GetTempPath(), PtySize.Default, "bad\0prompt"));
        Assert.Empty(pty.Requests);
    }

    [Theory]
    [MemberData(nameof(AgentDefinitionTests.SpecialPrompts), MemberType = typeof(AgentDefinitionTests))]
    public async Task Start_PassesPromptToRealProcessUnchanged(string prompt)
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("This test runs a real process in a Linux pseudoterminal.");
            return;
        }

        // The script prints each argument it gets, hex-encoded so the terminal can't change it.
        var definition = new AgentDefinition(
            "Shell", "/bin/sh", "-c", """for a in "$@"; do printf '%s' "$a" | od -An -tx1 | tr -d ' \n'; echo '|'; done; echo END""", "sh")
        {
            PromptArguments = ["{prompt}"],
        };
        var launcher = new AgentLauncher(new LinuxPtyService(new TestProcessLauncher()));

        await using var agent = launcher.Start(definition, Path.GetTempPath(), PtySize.Default, prompt);
        var output = await new PtyOutput(agent.Session).ReadUntilAsync("END");

        var expected = prompt.Length == 0 ? "" : Convert.ToHexStringLower(System.Text.Encoding.UTF8.GetBytes(prompt)) + "|";
        Assert.Equal(expected, output[..output.IndexOf("END", StringComparison.Ordinal)].ReplaceLineEndings(""));
    }

    [Fact]
    public void ClaudeCode_RunsClaude()
    {
        Assert.Equal("Claude Code", AgentDefinition.ClaudeCode.Name);
        Assert.Equal("claude", AgentDefinition.ClaudeCode.Command);
        Assert.Contains(AgentDefinition.ClaudeCode, AgentDefinition.BuiltIn);
    }

    // Real processes on Linux, so the states are checked against real exit codes, signals and hang-ups.

    [Theory]
    [InlineData("exit 0", AgentState.Exited, 0)]
    [InlineData("exit 3", AgentState.Error, 3)]
    [InlineData("kill -KILL $$", AgentState.Error, 137)]
    public async Task Start_FollowsRealProcess(string script, AgentState expectedState, int expectedExitCode)
    {
        await using var agent = StartShell(script);

        Assert.Equal(expectedState, await agent.Ended.WaitAsync(Timeout, TestContext.Current.CancellationToken));
        Assert.Equal(expectedExitCode, agent.ExitCode);
    }

    [Fact]
    public async Task StopAsync_StopsRealProcess()
    {
        await using var agent = StartShell("echo ready; sleep 60");
        await new PtyOutput(agent.Session).ReadUntilAsync("ready");

        await agent.StopAsync();

        Assert.Equal(AgentState.Exited, agent.State);
        Assert.True(agent.StopRequested);
    }

    [Fact]
    public async Task StopAsync_KillsRealProcessThatIgnoresHangUp()
    {
        await using var agent = StartShell("trap '' HUP; echo ready; while :; do sleep 0.1; done");
        await new PtyOutput(agent.Session).ReadUntilAsync("ready");

        await agent.StopAsync();

        Assert.Equal(AgentState.Exited, agent.State);
    }

    private static Agent StartShell(string script)
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("These tests run real processes in Linux pseudoterminals.");
            return null!;
        }

        var launcher = new AgentLauncher(new LinuxPtyService(new TestProcessLauncher()));
        var definition = new AgentDefinition("Shell", "/bin/sh", "-c", script);
        return launcher.Start(definition, Path.GetTempPath(), PtySize.Default);
    }

    private sealed class RecordingPtyService : IPtyService
    {
        public List<(ProcessRequest Request, PtySize Size)> Requests { get; } = [];

        public ProcessRequest GetDefaultShell() => new("sh");

        public IPtySession Start(ProcessRequest request, PtySize size)
        {
            Requests.Add((request, size));
            return new FakePtySession();
        }
    }
}
