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
    public void Start_WhenAgentCanResume_SetsNewSessionId()
    {
        var pty = new RecordingPtyService();
        var launcher = new AgentLauncher(pty);

        var first = launcher.Start(Resumable, Path.GetTempPath(), PtySize.Default, "do it");
        var second = launcher.Start(Resumable, Path.GetTempPath(), PtySize.Default);

        Assert.True(AgentSession.IsValidId(first.SessionId));
        Assert.True(AgentSession.IsValidId(second.SessionId));
        Assert.NotEqual(first.SessionId, second.SessionId);
        Assert.Equal(["-a", "--id", first.SessionId!, "do it"], pty.Requests[0].Request.Arguments);
        Assert.Equal(["-a", "--id", second.SessionId!], pty.Requests[1].Request.Arguments);
    }

    [Fact]
    public void Start_WhenAgentCantResume_HasNoSessionId()
    {
        var pty = new RecordingPtyService();

        var agent = new AgentLauncher(pty).Start(new AgentDefinition("Test", "agent", "-a"), Path.GetTempPath(), PtySize.Default);

        Assert.Null(agent.SessionId);
        Assert.Equal(["-a"], Assert.Single(pty.Requests).Request.Arguments);
    }

    [Fact]
    public void Start_WhenSessionIdComesFromHooks_HasNoSessionIdYet()
    {
        var pty = new RecordingPtyService();
        var definition = AgentDefinition.ClaudeCode with { SessionIdArguments = [] };

        var agent = new AgentLauncher(pty).Start(definition, Path.GetTempPath(), PtySize.Default, "do it");

        Assert.Null(agent.SessionId);
        Assert.Equal(["do it"], Assert.Single(pty.Requests).Request.Arguments);
    }

    [Fact]
    public void Resume_StartsResumeCommandInSessionsFolder()
    {
        var pty = new RecordingPtyService();
        var directory = Path.GetTempPath();
        var session = new AgentSession(Guid.NewGuid().ToString(), "Test", directory, DateTimeOffset.UnixEpoch);

        var agent = new AgentLauncher(pty).Resume(Resumable, session, new PtySize(100, 30));

        var request = Assert.Single(pty.Requests);
        Assert.Equal("agent", request.Request.FileName);
        Assert.Equal(["-a", "--resume", session.Id], request.Request.Arguments);
        Assert.Equal(Path.GetFullPath(directory), request.Request.WorkingDirectory);
        Assert.Equal(new PtySize(100, 30), request.Size);
        Assert.Equal(session.Id, agent.SessionId);
        Assert.Same(Resumable, agent.Definition);
    }

    [Fact]
    public void Resume_WhenAgentCantResume_Throws()
    {
        var pty = new RecordingPtyService();
        var session = new AgentSession(Guid.NewGuid().ToString(), "Test", Path.GetTempPath(), DateTimeOffset.UnixEpoch);

        Assert.Throws<ArgumentException>(() => new AgentLauncher(pty).Resume(
            Resumable with { ResumeArguments = [] }, session, PtySize.Default));
        Assert.Empty(pty.Requests);
    }

    [Theory]
    [InlineData("--dangerously-skip-permissions")]
    [InlineData("not-a-uuid")]
    [InlineData("")]
    public void Resume_WithIdTammiCouldntHaveSet_Throws(string id)
    {
        var pty = new RecordingPtyService();
        var session = new AgentSession(id, "Test", Path.GetTempPath(), DateTimeOffset.UnixEpoch);

        Assert.Throws<ArgumentException>(() => new AgentLauncher(pty).Resume(Resumable, session, PtySize.Default));
        Assert.Empty(pty.Requests);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Start_WithoutCommand_ThrowsSayingWhich(string command)
    {
        var pty = new RecordingPtyService();

        var exception = Assert.Throws<ArgumentException>(() => new AgentLauncher(pty).Start(
            new AgentDefinition("My agent", command), Path.GetTempPath(), PtySize.Default));

        Assert.Contains("My agent", exception.Message);
        Assert.Empty(pty.Requests);
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
        Assert.Equal([AgentDefinition.PromptPlaceholder], AgentDefinition.ClaudeCode.PromptArguments);
        Assert.Same(ClaudeCodeHooks.Instance, AgentDefinition.ClaudeCode.Hooks);
    }

    [Fact]
    public async Task Start_WithHooks_PassesThemBeforeArgumentsAndTokenInEnvironment()
    {
        var pty = new RecordingPtyService();
        await using var server = new AgentHookServer();
        var definition = AgentDefinition.ClaudeCode with { Arguments = ["--model", "opus"] };

        var agent = new AgentLauncher(pty, server).Start(definition, Path.GetTempPath(), PtySize.Default, "Hello");

        var request = Assert.Single(pty.Requests).Request;
        Assert.Equal("--settings", request.Arguments[0]);
        Assert.Equal(["--model", "opus", "--session-id", agent.SessionId!, "Hello"], request.Arguments.Skip(2));
        var token = Assert.IsType<string>(request.Environment[AgentHookEndpoint.TokenVariable]);
        Assert.DoesNotContain(request.Arguments, argument => argument.Contains(token, StringComparison.Ordinal));
    }

    [Fact]
    public void Start_WithoutHookServer_StartsWithoutHooks()
    {
        var pty = new RecordingPtyService();

        var agent = new AgentLauncher(pty).Start(AgentDefinition.ClaudeCode, Path.GetTempPath(), PtySize.Default, "Hello");

        var request = Assert.Single(pty.Requests).Request;
        Assert.Equal(["--session-id", agent.SessionId!, "Hello"], request.Arguments);
        Assert.Empty(request.Environment);
    }

    [Fact]
    public async Task Start_WithDisposedHookServer_StartsWithoutHooks()
    {
        var pty = new RecordingPtyService();
        var server = new AgentHookServer();
        await server.DisposeAsync();

        var agent = new AgentLauncher(pty, server).Start(AgentDefinition.ClaudeCode, Path.GetTempPath(), PtySize.Default);

        Assert.Equal(["--session-id", agent.SessionId!], Assert.Single(pty.Requests).Request.Arguments);
        Assert.Equal(AgentState.Running, agent.State);
    }

    [Fact]
    public async Task Start_DefinitionWithoutHooks_GetsNoHooks()
    {
        var pty = new RecordingPtyService();
        await using var server = new AgentHookServer();

        new AgentLauncher(pty, server).Start(
            new AgentDefinition("Test", "test-agent", "--flag"), Path.GetTempPath(), PtySize.Default);

        var request = Assert.Single(pty.Requests).Request;
        Assert.Equal(["--flag"], request.Arguments);
        Assert.Empty(request.Environment);
    }

    [Fact]
    public async Task Start_FollowsHookEventsFromRealProcess()
    {
        if (!OperatingSystem.IsLinux() || new TestProcessLauncher().FindExecutable("curl") is null)
        {
            Assert.Skip("This test runs a real process with curl in a Linux pseudoterminal.");
            return;
        }

        await using var server = new AgentHookServer();
        var launcher = new AgentLauncher(new LinuxPtyService(new TestProcessLauncher()), server);
        var definition = new AgentDefinition("Shell", "/bin/sh") { Hooks = new CurlHooks() };
        await using var agent = launcher.Start(definition, Path.GetTempPath(), PtySize.Default);
        var idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        agent.StateChanged += changed =>
        {
            if (changed.State == AgentState.Idle)
            {
                idle.TrySetResult();
            }
        };

        // Each event is sent once the previous one has been answered, as Claude Code does.
        await new PtyOutput(agent.Session).ReadUntilAsync("ready");
        agent.Session.Write("go\n"u8);

        await idle.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        Assert.Equal(AgentState.Idle, agent.State);
    }

    // Runs a script that sends Claude Code's events with curl, using the endpoint and token it was started with.
    private sealed class CurlHooks : AgentHooks
    {
        public override IReadOnlyList<string> CreateArguments(AgentHookEndpoint endpoint) =>
        [
            "-c",
            """
            echo ready; read line
            for e in UserPromptSubmit PermissionRequest PostToolUse Stop; do
              curl -sf -H "Authorization: Bearer $TAMMI_AGENT_HOOK_TOKEN" -H 'Content-Type: application/json' \
                --data "{\"hook_event_name\":\"$e\"}" "$1" || exit 1
            done
            sleep 60
            """,
            "sh",
            endpoint.Url.AbsoluteUri,
        ];

        public override AgentState? StateAfter(System.Text.Json.JsonElement hookEvent) =>
            ClaudeCodeHooks.Instance.StateAfter(hookEvent);
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

    private static AgentDefinition Resumable { get; } = new("Test", "agent", "-a")
    {
        PromptArguments = ["{prompt}"],
        SessionIdArguments = ["--id", "{sessionId}"],
        ResumeArguments = ["--resume", "{sessionId}"],
    };

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
}
