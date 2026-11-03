using System.Text.Json;
using TAMMi.Core.Agents;

namespace TAMMi.Core.Tests.Agents;

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

    public ValueTask DisposeAsync() => _server.DisposeAsync();

    private (Agent Agent, AgentHookRegistration Hooks) CreateAgent(FakePtySession session)
    {
        var hooks = _server.Register();
        return (new Agent(AgentDefinition.ClaudeCode, "/work", session, hooks), hooks);
    }

    private static void Send(AgentHookRegistration hooks, string eventName)
    {
        using var hookEvent = JsonDocument.Parse(JsonSerializer.Serialize(new { hook_event_name = eventName }));
        hooks.OnEventReceived(hookEvent.RootElement);
    }
}
