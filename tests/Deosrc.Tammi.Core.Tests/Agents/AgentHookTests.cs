using System.Text.Json;
using Deosrc.Tammi.Core.Agents;

namespace Deosrc.Tammi.Core.Tests.Agents;

/// <summary>
/// How an agent follows the events its hooks send.
/// </summary>
public sealed class AgentHookTests : IAsyncDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly AgentHookServer _server = new();

    [Fact]
    public void State_IsRunning_UntilFirstEvent()
    {
        var (agent, _) = CreateAgent(new FakePtySession());

        Assert.Equal(AgentState.Running, agent.State);
    }

    [Fact]
    public void State_FollowsEvents()
    {
        var (agent, hooks) = CreateAgent(new FakePtySession());

        Send(hooks, "UserPromptSubmit");
        Assert.Equal(AgentState.Working, agent.State);
        Send(hooks, "PermissionRequest");
        Assert.Equal(AgentState.NeedsInput, agent.State);
        Send(hooks, "PostToolUse");
        Assert.Equal(AgentState.Working, agent.State);
        Send(hooks, "Stop");
        Assert.Equal(AgentState.Idle, agent.State);
    }

    [Fact]
    public void StateChanged_IsRaisedOnlyWhenStateChanges()
    {
        var (agent, hooks) = CreateAgent(new FakePtySession());
        var states = new List<AgentState>();
        agent.StateChanged += changed => states.Add(changed.State);

        Send(hooks, "UserPromptSubmit");
        Send(hooks, "PostToolUse");
        Send(hooks, "SessionEnd");
        Send(hooks, "Stop");

        Assert.Equal([AgentState.Working, AgentState.Idle], states);
    }

    [Fact]
    public async Task Events_AfterProcessExits_AreIgnored()
    {
        var session = new FakePtySession();
        var (agent, hooks) = CreateAgent(session);
        Send(hooks, "UserPromptSubmit");

        session.Exit(0);
        await agent.Ended.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        Send(hooks, "Stop");

        Assert.Equal(AgentState.Exited, agent.State);
    }

    [Fact]
    public async Task ProcessExit_UnregistersHooks()
    {
        var session = new FakePtySession();
        var (agent, hooks) = CreateAgent(session);
        using var client = new HttpClient();

        session.Exit(0);
        await agent.Ended.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, hooks.Endpoint.Url)
        {
            Content = new StringContent("""{"hook_event_name":"Stop"}"""),
        };
        request.Headers.Authorization = new("Bearer", hooks.Endpoint.Token);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("UserPromptSubmit")]
    [InlineData("PermissionRequest")]
    [InlineData("Stop")]
    public async Task StopAsync_FromHookState_IsExited(string lastEvent)
    {
        var session = new FakePtySession();
        var (agent, hooks) = CreateAgent(session);
        Send(hooks, lastEvent);

        await agent.StopAsync();

        Assert.True(agent.StopRequested);
        Assert.Equal(AgentState.Exited, agent.State);
    }

    [Fact]
    public async Task ProcessError_FromHookState_IsError()
    {
        var session = new FakePtySession();
        var (agent, hooks) = CreateAgent(session);
        Send(hooks, "UserPromptSubmit");

        session.Exit(1);

        Assert.Equal(AgentState.Error, await agent.Ended.WaitAsync(Timeout, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void DefinitionWithoutHooks_IgnoresEventsAndUnregisters()
    {
        var hooks = _server.Register();
        var agent = new Agent(new AgentDefinition("Plain", "plain"), "/work", new FakePtySession(), hooks);

        Send(hooks, "UserPromptSubmit");

        Assert.Equal(AgentState.Running, agent.State);
    }

    [Fact]
    public void SessionId_FollowsSessionHooksReport()
    {
        var launchId = Guid.NewGuid().ToString();
        var clearedId = Guid.NewGuid().ToString();
        var hooks = _server.Register();
        var agent = new Agent(AgentDefinition.ClaudeCode, "/work", new FakePtySession(), hooks, launchId);
        var changes = new List<string?>();
        agent.SessionIdChanged += changed => changes.Add(changed.SessionId);

        Send(hooks, "UserPromptSubmit", launchId);
        Send(hooks, "Stop", launchId);
        // As after Claude Code's /clear, which starts another session in the same process.
        Send(hooks, "UserPromptSubmit", clearedId);

        Assert.Equal(clearedId, agent.SessionId);
        Assert.Equal([clearedId], changes);
    }

    [Fact]
    public void SessionId_WithoutIdAtLaunch_ComesFromHooks()
    {
        var id = Guid.NewGuid().ToString();
        var definition = AgentDefinition.ClaudeCode with { SessionIdArguments = [] };
        var hooks = _server.Register();
        var agent = new Agent(definition, "/work", new FakePtySession(), hooks);
        Assert.True(definition.CanResume);
        Assert.Null(agent.SessionId);

        Send(hooks, "UserPromptSubmit", id);

        Assert.Equal(id, agent.SessionId);
    }

    [Theory]
    [InlineData("--dangerously-skip-permissions")]
    [InlineData("not-a-uuid")]
    [InlineData("")]
    public void SessionId_ReportedThatCantBeResumed_IsIgnored(string reported)
    {
        var launchId = Guid.NewGuid().ToString();
        var hooks = _server.Register();
        var agent = new Agent(AgentDefinition.ClaudeCode, "/work", new FakePtySession(), hooks, launchId);

        Send(hooks, "UserPromptSubmit", reported);

        Assert.Equal(launchId, agent.SessionId);
    }

    [Fact]
    public void SessionId_WhenDefinitionCantResume_IsntTakenFromHooks()
    {
        var hooks = _server.Register();
        var definition = AgentDefinition.ClaudeCode with { ResumeArguments = [] };
        var agent = new Agent(definition, "/work", new FakePtySession(), hooks);

        Send(hooks, "UserPromptSubmit", Guid.NewGuid().ToString());

        Assert.Null(agent.SessionId);
    }

    public ValueTask DisposeAsync() => _server.DisposeAsync();

    private (Agent Agent, AgentHookRegistration Hooks) CreateAgent(FakePtySession session)
    {
        var hooks = _server.Register();
        return (new Agent(AgentDefinition.ClaudeCode, "/work", session, hooks), hooks);
    }

    private static void Send(AgentHookRegistration hooks, string eventName, string? sessionId = null)
    {
        object body = sessionId is null
            ? new { hook_event_name = eventName }
            : new { hook_event_name = eventName, session_id = sessionId };
        using var hookEvent = JsonDocument.Parse(JsonSerializer.Serialize(body));
        hooks.OnEventReceived(hookEvent.RootElement);
    }
}
