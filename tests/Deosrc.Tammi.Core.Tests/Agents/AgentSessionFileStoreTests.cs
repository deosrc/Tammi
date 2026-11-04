using System.Text.Json.Nodes;
using TAMMi.Core.Agents;
using TAMMi.Core.Platform;
using TAMMi.Core.Tests.Platform.Pty;

namespace TAMMi.Core.Tests.Agents;

public sealed class AgentSessionFileStoreTests : IDisposable
{
    private static readonly DateTimeOffset StartedAt = new(2026, 11, 3, 9, 30, 0, TimeSpan.Zero);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "tammi-tests", Guid.NewGuid().ToString("N"));

    // A folder that doesn't exist yet, as in a new workspace.
    private string FilePath => Path.Combine(_root, ".tammi", AgentSessionFileStore.DefaultFileName);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task ListAsync_WithoutFile_ReturnsNothingWithoutWritingFile()
    {
        var store = CreateStore();

        Assert.Empty(await store.ListAsync(Token));
        Assert.Null(store.Problem);
        Assert.False(File.Exists(FilePath));
    }

    [Fact]
    public async Task RecordAsync_WritesSessionThatANewStoreLists()
    {
        var session = CreateSession(StartedAt);

        await CreateStore().RecordAsync(session, Token);

        Assert.Equal([session], await CreateStore().ListAsync(Token));
    }

    [Fact]
    public async Task RecordAsync_WritesVersionedFile()
    {
        var session = CreateSession(StartedAt);

        await CreateStore().RecordAsync(session, Token);

        var json = JsonNode.Parse(await File.ReadAllTextAsync(FilePath, Token))!.AsObject();
        Assert.Equal(["version", "sessions"], json.Select(p => p.Key));
        Assert.Equal(AgentSessionFileStore.CurrentVersion, json["version"]!.GetValue<int>());
        var written = Assert.Single(json["sessions"]!.AsArray())!.AsObject();
        Assert.Equal(["id", "agentName", "workingDirectory", "lastStartedAt"], written.Select(p => p.Key));
    }

    [Fact]
    public async Task RecordAsync_KeepsOtherSessions_MostRecentlyStartedFirst()
    {
        var store = CreateStore();
        var older = CreateSession(StartedAt);
        var newer = CreateSession(StartedAt.AddHours(1));
        var oldest = CreateSession(StartedAt.AddHours(-1));

        await store.RecordAsync(older, Token);
        await store.RecordAsync(newer, Token);
        await store.RecordAsync(oldest, Token);

        Assert.Equal([newer, older, oldest], await CreateStore().ListAsync(Token));
    }

    [Fact]
    public async Task RecordAsync_WithSameId_ReplacesSession()
    {
        var store = CreateStore();
        var session = CreateSession(StartedAt);
        var other = CreateSession(StartedAt.AddHours(1));
        await store.RecordAsync(session, Token);
        await store.RecordAsync(other, Token);

        // Resuming the session records it again.
        var resumed = session with { LastStartedAt = StartedAt.AddHours(2) };
        await store.RecordAsync(resumed, Token);

        Assert.Equal([resumed, other], await store.ListAsync(Token));
    }

    [Fact]
    public async Task RecordAsync_FromSeveralCallersAtOnce_KeepsEverySession()
    {
        var store = CreateStore();
        var sessions = Enumerable.Range(0, 20).Select(i => CreateSession(StartedAt.AddMinutes(i))).ToArray();

        await Task.WhenAll(sessions.Select(session => Task.Run(() => store.RecordAsync(session, Token), Token)));

        Assert.Equal(sessions.Reverse(), await store.ListAsync(Token));
    }

    [Fact]
    public async Task ForgetAsync_RemovesSession()
    {
        var store = CreateStore();
        var forgotten = CreateSession(StartedAt);
        var kept = CreateSession(StartedAt.AddHours(1));
        await store.RecordAsync(forgotten, Token);
        await store.RecordAsync(kept, Token);

        await store.ForgetAsync(forgotten.Id, Token);

        Assert.Equal([kept], await CreateStore().ListAsync(Token));
    }

    [Fact]
    public async Task ForgetAsync_UnknownSession_DoesNothing()
    {
        var store = CreateStore();

        await store.ForgetAsync(Guid.NewGuid().ToString(), Token);

        Assert.False(File.Exists(FilePath));
    }

    [Fact]
    public async Task ListAsync_WithInvalidFile_ReportsProblemAndListsNothing()
    {
        WriteFile("{ not json");
        var store = CreateStore();

        Assert.Empty(await store.ListAsync(Token));
        Assert.NotNull(store.Problem);
        Assert.Contains(FilePath, store.Problem);
    }

    [Fact]
    public async Task RecordAsync_WithInvalidFile_KeepsCopyBeforeReplacingIt()
    {
        WriteFile("{ not json");
        var store = CreateStore();
        var session = CreateSession(StartedAt);

        await store.RecordAsync(session, Token);

        Assert.Equal("{ not json", await File.ReadAllTextAsync(store.InvalidFilePath, Token));
        Assert.Equal(Path.Combine(_root, ".tammi", "agent-sessions.invalid.json"), store.InvalidFilePath);
        Assert.Equal([session], await store.ListAsync(Token));
        Assert.Null(store.Problem);
    }

    [Fact]
    public async Task ListAsync_WithIncompleteSessions_LeavesThemOut()
    {
        var id = Guid.NewGuid().ToString();
        WriteFile($$"""
            {
              "version": 1,
              "sessions": [
                null,
                { "agentName": "No ID", "workingDirectory": "/ws", "lastStartedAt": "2026-11-03T09:30:00Z" },
                { "id": "{{Guid.NewGuid()}}", "agentName": "No folder", "lastStartedAt": "2026-11-03T09:30:00Z" },
                { "id": "{{id}}", "agentName": "Claude Code", "workingDirectory": "/ws", "lastStartedAt": "2026-11-03T09:30:00Z" }
              ]
            }
            """);

        var session = Assert.Single(await CreateStore().ListAsync(Token));

        Assert.Equal(new AgentSession(id, "Claude Code", "/ws", StartedAt), session);
    }

    [Fact]
    public async Task ListAsync_WithoutSessionsOrVersion_ListsNothing()
    {
        WriteFile("{}");
        var store = CreateStore();

        Assert.Empty(await store.ListAsync(Token));
        Assert.Null(store.Problem);
    }

    [Fact]
    public async Task ListAsync_ReadsChangesFromAnotherStore()
    {
        var store = CreateStore();
        Assert.Empty(await store.ListAsync(Token));
        var session = CreateSession(StartedAt);

        await CreateStore().RecordAsync(session, Token);

        Assert.Equal([session], await store.ListAsync(Token));
    }

    [Fact]
    public async Task RecordedSession_CanBeResumedByANewLauncher()
    {
        // As after a restart: the session recorded when the agent started is read back and resumed.
        var pty = new RecordingPtyService();
        var started = new AgentLauncher(pty).Start(AgentDefinition.ClaudeCode, Path.GetTempPath(), PtySize.Default, "Read tasks.md");
        await CreateStore().RecordAsync(AgentSession.Of(started, StartedAt)!, Token);

        var recorded = Assert.Single(await CreateStore().ListAsync(Token));
        var definition = recorded.FindDefinition([AgentDefinition.ClaudeCode])!;
        var resumed = new AgentLauncher(pty).Resume(definition, recorded, PtySize.Default);

        Assert.Equal(started.SessionId, resumed.SessionId);
        Assert.Equal(["--session-id", started.SessionId!, "Read tasks.md"], pty.Requests[0].Request.Arguments);
        Assert.Equal(["--resume", started.SessionId!], pty.Requests[1].Request.Arguments);
        Assert.Equal(started.WorkingDirectory, pty.Requests[1].Request.WorkingDirectory);
    }

    private AgentSessionFileStore CreateStore() => new(FilePath);

    private static AgentSession CreateSession(DateTimeOffset startedAt) =>
        new(Guid.NewGuid().ToString(), "Claude Code", "/ws/project", startedAt);

    private void WriteFile(string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, contents);
    }
}
