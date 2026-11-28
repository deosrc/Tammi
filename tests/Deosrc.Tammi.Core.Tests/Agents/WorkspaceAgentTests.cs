using System.Text.Json;
using Deosrc.Tammi.Core.Agents;

namespace Deosrc.Tammi.Core.Tests.Agents;

public sealed class WorkspaceAgentTests : IAsyncDisposable
{
    private static readonly DateTimeOffset StartedAt = new(2026, 11, 3, 9, 30, 0, TimeSpan.Zero);

    private readonly AgentHookServer _server = new();

    public ValueTask DisposeAsync() => _server.DisposeAsync();

    [Fact]
    public async Task NoteNewSession_AfterTheAgentMovedToAnotherSession_ReturnsTheOldOneAsEnded()
    {
        var oldId = Guid.NewGuid().ToString();
        var newId = Guid.NewGuid().ToString();
        var hooks = _server.Register();
        await using var agent = new Agent(AgentDefinition.ClaudeCode, "/ws", new FakePtySession(), hooks, oldId);
        var workspaceAgent = new WorkspaceAgent("ws", "Claude Code", agent, StartedAt, Resumed(oldId));
        workspaceAgent.SessionToRecord();

        // As after Claude Code's /clear, which starts another session in the same process.
        Send(hooks, newId);
        var ended = workspaceAgent.NoteNewSession(StartedAt.AddHours(1));

        Assert.Equal(
            new AgentSession(
                oldId,
                "Claude Code",
                "/ws",
                StartedAt,
                CreatedAt: StartedAt.AddDays(-1),
                LastEndedAt: StartedAt.AddHours(1),
                LastTitle: "Fix the bug"),
            ended);
        Assert.Equal(StartedAt.AddHours(1), workspaceAgent.SessionStartedAt);
        Assert.Null(workspaceAgent.LastTitle);
        Assert.Equal(
            new AgentSession(newId, "Claude Code", "/ws", StartedAt.AddHours(1), CreatedAt: StartedAt.AddHours(1)),
            workspaceAgent.SessionToRecord());
    }

    [Fact]
    public async Task NoteNewSession_BeforeAnySessionWasRecorded_ReturnsNothing()
    {
        var hooks = _server.Register();
        await using var agent = new Agent(AgentDefinition.ClaudeCode, "/ws", new FakePtySession(), hooks);
        var workspaceAgent = new WorkspaceAgent("ws", "Claude Code", agent, StartedAt);

        Send(hooks, Guid.NewGuid().ToString());

        Assert.Null(workspaceAgent.NoteNewSession(StartedAt.AddHours(1)));
    }

    [Fact]
    public async Task SessionToRecord_ForANewSession_RecordsItAsCreatedWhenItStarted_AndWhenItEnded()
    {
        var id = Guid.NewGuid().ToString();
        await using var agent = new Agent(AgentDefinition.ClaudeCode, "/ws", new FakePtySession(), sessionId: id);
        var workspaceAgent = new WorkspaceAgent("ws", "Claude Code", agent, StartedAt);

        workspaceAgent.NoteEnded(StartedAt.AddHours(1));

        Assert.Equal(
            new AgentSession(id, "Claude Code", "/ws", StartedAt, CreatedAt: StartedAt, LastEndedAt: StartedAt.AddHours(1)),
            workspaceAgent.SessionToRecord());
    }

    [Fact]
    public async Task SessionToRecord_ForAResumedSession_KeepsWhenItWasCreatedAndItsTitle()
    {
        var id = Guid.NewGuid().ToString();
        await using var agent = new Agent(AgentDefinition.ClaudeCode, "/ws", new FakePtySession(), sessionId: id);
        var workspaceAgent = new WorkspaceAgent("ws", "Claude Code", agent, StartedAt, Resumed(id));

        Assert.Equal(
            new AgentSession(id, "Claude Code", "/ws", StartedAt, CreatedAt: StartedAt.AddDays(-1), LastTitle: "Fix the bug"),
            workspaceAgent.SessionToRecord());
    }

    [Fact]
    public async Task SessionToRecord_ForAResumedSessionWithoutCreationTime_LeavesItUnknown()
    {
        var id = Guid.NewGuid().ToString();
        await using var agent = new Agent(AgentDefinition.ClaudeCode, "/ws", new FakePtySession(), sessionId: id);
        var workspaceAgent = new WorkspaceAgent(
            "ws", "Claude Code", agent, StartedAt, Resumed(id) with { CreatedAt = null });

        Assert.Null(workspaceAgent.SessionToRecord()!.CreatedAt);
        Assert.Null(workspaceAgent.SessionCreatedAt);
    }

    // A session first started a day before it's resumed, and ended an hour after that.
    private static AgentSession Resumed(string id) => new(
        id,
        "Claude Code",
        "/ws",
        StartedAt.AddDays(-1),
        CreatedAt: StartedAt.AddDays(-1),
        LastEndedAt: StartedAt.AddDays(-1).AddHours(1),
        LastTitle: "Fix the bug");

    private static void Send(AgentHookRegistration hooks, string sessionId)
    {
        using var hookEvent = JsonDocument.Parse(JsonSerializer.Serialize(
            new { hook_event_name = "UserPromptSubmit", session_id = sessionId }));
        hooks.OnEventReceived(hookEvent.RootElement);
    }
}
