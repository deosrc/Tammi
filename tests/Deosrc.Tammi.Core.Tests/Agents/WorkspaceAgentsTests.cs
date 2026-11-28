using Microsoft.Extensions.Time.Testing;
using Deosrc.Tammi.Core.Agents;
using Deosrc.Tammi.Core.Integrations;
using Deosrc.Tammi.Core.Settings;
using Deosrc.Tammi.Core.Terminals;
using Deosrc.Tammi.Core.Tests.Platform.Pty;
using Deosrc.Tammi.Core.Tests.Settings;
using Deosrc.Tammi.Core.Workspaces;

namespace Deosrc.Tammi.Core.Tests.Agents;

public sealed class WorkspaceAgentsTests : IAsyncDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static readonly AgentDefinition Resumable = new("Resumable", "resumable-agent", "-a")
    {
        SessionIdArguments = ["--id", AgentDefinition.SessionIdPlaceholder],
        ResumeArguments = ["--resume", AgentDefinition.SessionIdPlaceholder],
    };

    private static readonly AgentDefinition Plain = new("Plain", "plain-agent");

    private static readonly AgentDefinition Prompted = new("Prompted", "prompted-agent", "-a")
    {
        PromptArguments = ["--prompt", AgentDefinition.PromptPlaceholder],
        SessionIdArguments = ["--id", AgentDefinition.SessionIdPlaceholder],
        ResumeArguments = ["--resume", AgentDefinition.SessionIdPlaceholder],
    };

    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("tammi-tests-");
    private readonly RecordingPtyService _pty = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero));
    private readonly InMemorySettingsStore _settings = new(new AppSettings
    {
        Agents = new AgentSettings
        {
            CustomDefinitions = [Resumable, Plain, Prompted],
            InitialPromptTemplate = "Read {contextFile} first.",
            InitialPromptStyle = InitialPromptStyle.Submit,
        },
    });

    private readonly WorkspaceAgents _agents;
    private readonly Workspace _workspace;
    private readonly Workspace _other;

    public WorkspaceAgentsTests()
    {
        _agents = new WorkspaceAgents(new AgentLauncher(_pty), _settings, _time);
        _workspace = CreateWorkspace("one");
        _other = CreateWorkspace("two");
    }

    public async ValueTask DisposeAsync()
    {
        await _agents.DisposeAsync();
        _root.Delete(recursive: true);
    }

    [Fact]
    public async Task StartAsync_ByDefault_StartsInTheWorkspaceFolder()
    {
        var agent = await _agents.StartAsync(_workspace, Plain);

        var request = Assert.Single(_pty.Requests).Request;
        Assert.Equal("plain-agent", request.FileName);
        Assert.Equal(_workspace.Folder, request.WorkingDirectory);
        Assert.Equal(_workspace.Folder, agent.WorkingDirectory);
        Assert.Equal(_workspace.Id, agent.WorkspaceId);
        Assert.Equal("Plain", agent.Name);
        Assert.Equal(AgentState.Running, agent.State);
        Assert.False(agent.HasExited);
    }

    [Fact]
    public async Task StartAsync_InAStandaloneRepository_RecordsItsSessionInTheDataFolder_AndGivesNoInitialPrompt()
    {
        var data = Path.Combine(_root.FullName, "data");
        var repository = Path.Combine(_root.FullName, "repos", "api");
        Directory.CreateDirectory(repository);
        await using var agents = new WorkspaceAgents(new AgentLauncher(_pty), _settings, _time, appPaths: new TestAppPaths(data));
        var workspace = StandaloneRepository.WorkspaceOf(repository);

        var agent = await agents.StartAsync(workspace, Prompted, workspace.Repositories[0]);

        var request = Assert.Single(_pty.Requests).Request;
        Assert.Equal(repository, request.WorkingDirectory);
        Assert.DoesNotContain("--prompt", request.Arguments);
        Assert.Equal(workspace.Id, agent.WorkspaceId);
        Assert.Single(await agents.SessionsOf(workspace).ListAsync(TestContext.Current.CancellationToken));
        Assert.True(Directory.Exists(Path.Combine(data, "repository-agent-sessions")));
        // Nothing is added beside the repository.
        Assert.False(Directory.Exists(workspace.MetadataFolder));
    }

    private sealed class TestAppPaths(string dataDirectory) : Deosrc.Tammi.Core.Platform.IAppPaths
    {
        public string ConfigDirectory => throw new NotSupportedException();

        public string DataDirectory => dataDirectory;

        public string CacheDirectory => throw new NotSupportedException();

        public string LogDirectory => throw new NotSupportedException();
    }

    [Fact]
    public async Task StartAsync_InRepository_StartsInItsWorktreeAndIsNamedAfterIt()
    {
        var repository = _workspace.Repositories[0];

        var agent = await _agents.StartAsync(_workspace, Plain, repository);

        Assert.Equal(_workspace.GetWorktreePath(repository), Assert.Single(_pty.Requests).Request.WorkingDirectory);
        Assert.Equal("Plain (api)", agent.Name);
    }

    [Fact]
    public async Task StartAsync_NameInUse_AddsTheFirstFreeNumber()
    {
        await _agents.StartAsync(_workspace, Plain);
        var second = await _agents.StartAsync(_workspace, Plain);
        await _agents.StartAsync(_workspace, Resumable);
        await _agents.StartAsync(_other, Plain);
        await _agents.CloseAsync(second);
        await _agents.StartAsync(_workspace, Plain);
        await _agents.StartAsync(_workspace, Plain);

        Assert.Equal(
            ["Plain", "Resumable", "Plain 2", "Plain 3"],
            _agents.For(_workspace.Id).Select(agent => agent.Name));
        Assert.Equal("Plain", Assert.Single(_agents.For(_other.Id)).Name);
    }

    [Fact]
    public async Task StartAsync_DefinitionWithoutName_UsesTheDefaultName()
    {
        var agent = await _agents.StartAsync(_workspace, Plain with { Name = " " });

        Assert.Equal(WorkspaceAgents.DefaultName, agent.Name);
    }

    [Fact]
    public async Task StartAsync_RecordsTheSessionInTheWorkspace()
    {
        var agent = await _agents.StartAsync(_workspace, Resumable, _workspace.Repositories[0]);

        var session = Assert.Single(await _agents.SessionsOf(_workspace).ListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(agent.Agent.SessionId, session.Id);
        Assert.Equal("Resumable", session.AgentName);
        Assert.Equal(agent.WorkingDirectory, session.WorkingDirectory);
        Assert.Equal(_time.GetUtcNow(), session.LastStartedAt);
        Assert.True(File.Exists(Path.Combine(_workspace.MetadataFolder, AgentSessionFileStore.DefaultFileName)));
        Assert.Null(agent.SessionProblem);
        Assert.Empty(await _agents.SessionsOf(_other).ListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StartAsync_AgentThatCantResume_RecordsNothing()
    {
        await _agents.StartAsync(_workspace, Plain);

        Assert.Empty(await _agents.SessionsOf(_workspace).ListAsync(TestContext.Current.CancellationToken));
        Assert.False(File.Exists(Path.Combine(_workspace.MetadataFolder, AgentSessionFileStore.DefaultFileName)));
    }

    [Fact]
    public async Task StartAsync_SessionCantBeRecorded_StillRunsAndSaysWhy()
    {
        // A file where the .tammi folder should be stops the sessions file from being written.
        var workspace = CreateWorkspace("broken", createMetadataFolder: false);
        File.WriteAllText(workspace.MetadataFolder, "not a folder");

        var agent = await _agents.StartAsync(workspace, Resumable);

        Assert.False(agent.HasExited);
        Assert.Contains("couldn't be recorded", agent.SessionProblem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartAsync_CommandNotFound_Throws_AndAddsNothing()
    {
        var launcher = new AgentLauncher(new FailingPtyService(new FileNotFoundException("not found")));
        await using var agents = new WorkspaceAgents(launcher, _settings, _time);

        await Assert.ThrowsAsync<FileNotFoundException>(() => agents.StartAsync(_workspace, Plain));

        Assert.Empty(agents.For(_workspace.Id));
    }

    [Fact]
    public async Task StartAsync_RaisesChanged()
    {
        var changed = new List<string>();
        _agents.Changed += changed.Add;

        await _agents.StartAsync(_workspace, Plain);

        Assert.Equal([_workspace.Id], changed);
    }

    [Fact]
    public async Task ResumeAsync_ResumesWithTheDefinitionInTheSessionsFolder()
    {
        var repository = _workspace.Repositories[0];
        var started = await _agents.StartAsync(_workspace, Resumable, repository);
        await _agents.StopAsync(started);
        var session = Assert.Single(await _agents.SessionsOf(_workspace).ListAsync(TestContext.Current.CancellationToken));
        _time.Advance(TimeSpan.FromHours(1));

        var resumed = await _agents.ResumeAsync(_workspace, session);

        Assert.Equal(session.LastStartedAt, session.CreatedAt);
        var request = _pty.Requests[1].Request;
        Assert.Equal(["-a", "--resume", session.Id], request.Arguments);
        Assert.Equal(_workspace.GetWorktreePath(repository), request.WorkingDirectory);
        Assert.Equal(session.Id, resumed.Agent.SessionId);
        Assert.Equal("Resumable (api) 2", resumed.Name);
        var recorded = Assert.Single(await _agents.SessionsOf(_workspace).ListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(_time.GetUtcNow(), recorded.LastStartedAt);
        Assert.Equal(session.CreatedAt, recorded.CreatedAt);
        Assert.Null(recorded.LastEndedAt);
    }

    [Fact]
    public async Task StartAsync_AgentTakesAPrompt_SendsTheInitialPrompt_WithTheWorkspaceContextFile()
    {
        var workspace = _workspace with { Tasks = [new TaskReference("github", "acme/api#42")] };

        var agent = await _agents.StartAsync(workspace, Prompted);

        var contextFile = WorkspaceContextFile.PathOf(workspace);
        Assert.Equal(
            ["-a", "--id", agent.Agent.SessionId!, "--prompt", $"Read {contextFile} first."],
            Assert.Single(_pty.Requests).Request.Arguments);
        Assert.Contains(
            "## acme/api#42",
            await File.ReadAllTextAsync(contextFile, TestContext.Current.CancellationToken),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartAsync_Prefill_StartsWithoutThePrompt_ThenTypesItWithoutSendingIt()
    {
        await _settings.UpdateAsync(
            settings => settings with { Agents = settings.Agents with { InitialPromptStyle = InitialPromptStyle.Prefill } },
            TestContext.Current.CancellationToken);

        var agent = await _agents.StartAsync(_workspace, Prompted);

        Assert.Equal(["-a", "--id", agent.Agent.SessionId!], Assert.Single(_pty.Requests).Request.Arguments);
        Assert.Equal("", _pty.Sessions[0].Input);
        _time.Advance(WorkspaceAgents.PrefillDelay);
        var contextFile = WorkspaceContextFile.PathOf(_workspace);
        await WaitForAsync(() => _pty.Sessions[0].Input.Length > 0);
        Assert.Equal($"\u001b[200~Read {contextFile} first.\u001b[201~", _pty.Sessions[0].Input);
    }

    [Fact]
    public async Task StartAsync_PrefillAndAgentEndsFirst_TypesNothing()
    {
        await _settings.UpdateAsync(
            settings => settings with { Agents = settings.Agents with { InitialPromptStyle = InitialPromptStyle.Prefill } },
            TestContext.Current.CancellationToken);

        await _agents.StartAsync(_workspace, Prompted);
        _pty.Sessions[0].Exit(0);
        await _agents.For(_workspace.Id)[0].Agent.Ended;
        _time.Advance(WorkspaceAgents.PrefillDelay);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal("", _pty.Sessions[0].Input);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task StartAsync_WorkspaceContextFileExists_KeepsIt()
    {
        var contextFile = WorkspaceContextFile.PathOf(_workspace);
        await File.WriteAllTextAsync(contextFile, "Details", TestContext.Current.CancellationToken);

        await _agents.StartAsync(_workspace, Prompted);

        Assert.Equal("Details", await File.ReadAllTextAsync(contextFile, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StartAsync_AgentDoesntTakeAPrompt_StartsWithoutOne_OrTheFile()
    {
        await _agents.StartAsync(_workspace, Resumable);

        var arguments = Assert.Single(_pty.Requests).Request.Arguments;
        Assert.DoesNotContain(arguments, argument => argument.Contains("Read ", StringComparison.Ordinal));
        Assert.False(File.Exists(WorkspaceContextFile.PathOf(_workspace)));
    }

    [Fact]
    public async Task StartAsync_WorkspaceContextFileCantBeWritten_Throws_AndAddsNothing()
    {
        var workspace = CreateWorkspace("broken", createMetadataFolder: false);

        var ex = await Assert.ThrowsAsync<IOException>(() => _agents.StartAsync(workspace, Prompted));

        Assert.Contains("workspace context file", ex.Message, StringComparison.Ordinal);
        Assert.Empty(_pty.Requests);
        Assert.Empty(_agents.For(workspace.Id));
    }

    [Fact]
    public async Task ResumeAsync_DoesntSendTheInitialPromptAgain()
    {
        var started = await _agents.StartAsync(_workspace, Prompted);
        await _agents.StopAsync(started);
        var session = Assert.Single(await _agents.SessionsOf(_workspace).ListAsync(TestContext.Current.CancellationToken));

        await _agents.ResumeAsync(_workspace, session);

        Assert.Equal(["-a", "--resume", session.Id], _pty.Requests[1].Request.Arguments);
    }

    [Fact]
    public async Task ResumeAsync_NoAgentHasTheName_Throws()
    {
        var session = new AgentSession(Guid.NewGuid().ToString(), "Gone", _workspace.Folder, _time.GetUtcNow());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _agents.ResumeAsync(_workspace, session));

        Assert.Contains("Gone", ex.Message, StringComparison.Ordinal);
        Assert.Empty(_pty.Requests);
    }

    [Fact]
    public async Task ResumeAsync_SessionAlreadyRunning_Throws()
    {
        var agent = await _agents.StartAsync(_workspace, Resumable);
        var session = AgentSession.Of(agent.Agent, _time.GetUtcNow())!;

        await Assert.ThrowsAsync<InvalidOperationException>(() => _agents.ResumeAsync(_workspace, session));

        Assert.True(_agents.IsRunning(_workspace.Id, session.Id));
        Assert.False(_agents.IsRunning(_other.Id, session.Id));
        Assert.Single(_pty.Requests);
    }

    [Fact]
    public async Task ProcessExits_RaisesChangedAndKeepsTheAgent()
    {
        var agent = await _agents.StartAsync(_workspace, Plain);
        var exited = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _agents.Changed += id => exited.TrySetResult(id);

        _pty.Sessions[0].Exit(3);

        Assert.Equal(_workspace.Id, await exited.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken));
        Assert.Equal(AgentState.Error, agent.State);
        Assert.True(agent.HasExited);
        Assert.Same(agent, Assert.Single(_agents.For(_workspace.Id)));
        Assert.Equal(0, _agents.CountRunning());
    }

    [Fact]
    public async Task StopAsync_EndsTheProcessAsExited_AndKeepsTheAgent()
    {
        var agent = await _agents.StartAsync(_workspace, Plain);

        await _agents.StopAsync(agent);

        Assert.True(_pty.Sessions[0].Disposed);
        Assert.Equal(AgentState.Exited, agent.State);
        Assert.Same(agent, Assert.Single(_agents.For(_workspace.Id)));
    }

    [Fact]
    public async Task CloseAsync_StopsTheAgentAndRemovesIt()
    {
        var agent = await _agents.StartAsync(_workspace, Plain);
        var changed = new List<string>();
        _agents.Changed += changed.Add;

        await _agents.CloseAsync(agent);
        await _agents.CloseAsync(agent);

        Assert.True(_pty.Sessions[0].Disposed);
        Assert.Empty(_agents.For(_workspace.Id));
        Assert.Contains(_workspace.Id, changed);
    }

    [Fact]
    public async Task CountRunning_CountsRunningAgentsInEveryWorkspace()
    {
        var stopped = await _agents.StartAsync(_workspace, Plain);
        await _agents.StartAsync(_workspace, Plain);
        await _agents.StartAsync(_other, Plain);
        await _agents.StopAsync(stopped);

        Assert.Equal(2, _agents.CountRunning());
    }

    [Fact]
    public async Task CloseAllAsync_ClosesOnlyThatWorkspacesAgents()
    {
        await _agents.StartAsync(_workspace, Plain);
        await _agents.StartAsync(_other, Plain);
        await _agents.StartAsync(_workspace, Plain);

        await _agents.CloseAllAsync(_workspace.Id);

        Assert.Equal([true, false, true], _pty.Sessions.Select(session => session.Disposed));
        Assert.Empty(_agents.For(_workspace.Id));
        Assert.Single(_agents.For(_other.Id));
    }

    [Fact]
    public async Task DisposeAsync_StopsEveryAgent_AndStopsNewOnes()
    {
        await _agents.StartAsync(_workspace, Plain);
        await _agents.StartAsync(_other, Plain);

        await _agents.DisposeAsync();

        Assert.All(_pty.Sessions, session => Assert.True(session.Disposed));
        Assert.Equal(0, _agents.CountRunning());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => _agents.StartAsync(_workspace, Plain));
    }

    [Fact]
    public async Task StartOrder_FollowsTerminalsAndAgentsTogether()
    {
        await using var terminals = new WorkspaceTerminals(new TerminalLauncher(_pty, _settings));

        var first = await _agents.StartAsync(_workspace, Plain);
        var second = terminals.Start(_workspace);
        var third = await _agents.StartAsync(_workspace, Plain);

        Assert.True(first.StartOrder < second.StartOrder);
        Assert.True(second.StartOrder < third.StartOrder);
    }

    [Fact]
    public async Task TitleChanges_AreRecordedWithTheSession_AfterTheDelay()
    {
        var agent = await _agents.StartAsync(_workspace, Resumable);
        var startedAt = _time.GetUtcNow();
        _time.Advance(TimeSpan.FromMinutes(1));

        _pty.Sessions[0].Output("\u001b]0;First\u0007");
        await WaitForAsync(() => agent.LastTitle == "First");
        _pty.Sessions[0].Output("\u001b]0;Fix the bug\u0007");
        await WaitForAsync(() => agent.LastTitle == "Fix the bug");
        Assert.Null((await ListSessionsAsync()).Single().LastTitle);

        _time.Advance(WorkspaceAgents.TitleRecordDelay);

        var recorded = await WaitForSessionAsync(session => session.LastTitle == "Fix the bug");
        Assert.Equal(startedAt, recorded.LastStartedAt);
    }

    [Fact]
    public async Task AgentEnds_RecordsItsTitleAtOnce()
    {
        var agent = await _agents.StartAsync(_workspace, Resumable);
        _pty.Sessions[0].Output("\u001b]0;Fix the bug\u0007");
        await WaitForAsync(() => agent.LastTitle == "Fix the bug");

        _pty.Sessions[0].Exit(0);

        await WaitForSessionAsync(session => session.LastTitle == "Fix the bug");
    }

    [Fact]
    public async Task AgentEnds_RecordsWhenItsSessionEnded_AndResumingClearsIt()
    {
        var agent = await _agents.StartAsync(_workspace, Resumable);
        var startedAt = _time.GetUtcNow();
        Assert.Null(Assert.Single(await ListSessionsAsync()).LastEndedAt);
        _time.Advance(TimeSpan.FromMinutes(10));

        _pty.Sessions[0].Exit(0);

        var ended = await WaitForSessionAsync(session => session.LastEndedAt == _time.GetUtcNow());
        Assert.Equal(startedAt, ended.LastStartedAt);

        await _agents.ResumeAsync(_workspace, ended);

        Assert.Null(Assert.Single(await ListSessionsAsync()).LastEndedAt);
    }

    [Fact]
    public async Task CloseAsync_RecordsTheSessionsEndBeforeItCompletes()
    {
        var agent = await _agents.StartAsync(_workspace, Resumable);

        await _agents.CloseAsync(agent);

        Assert.Equal(_time.GetUtcNow(), Assert.Single(await ListSessionsAsync()).LastEndedAt);
    }

    [Fact]
    public async Task TitleCleared_KeepsTheLastTitle()
    {
        var agent = await _agents.StartAsync(_workspace, Resumable);
        _pty.Sessions[0].Output("\u001b]0;Fix the bug\u0007");
        await WaitForAsync(() => agent.LastTitle == "Fix the bug");

        _pty.Sessions[0].Output("\u001b]0;\u0007");
        await WaitForAsync(() => agent.Session.Title is null);

        Assert.Equal("Fix the bug", agent.LastTitle);
        Assert.Equal(agent.Name, agent.DisplayName);
    }

    [Fact]
    public async Task ResumeAsync_KeepsTheSessionsTitle()
    {
        var started = await _agents.StartAsync(_workspace, Resumable);
        _pty.Sessions[0].Output("\u001b]0;Fix the bug\u0007");
        await WaitForAsync(() => started.LastTitle == "Fix the bug");
        await _agents.StopAsync(started);
        var session = await WaitForSessionAsync(recorded => recorded.LastTitle == "Fix the bug");

        var resumed = await _agents.ResumeAsync(_workspace, session);

        Assert.Equal("Fix the bug", resumed.LastTitle);
        Assert.Null(resumed.Session.Title);
        Assert.Equal("Fix the bug", Assert.Single(await ListSessionsAsync()).LastTitle);
    }

    private Task<IReadOnlyList<AgentSession>> ListSessionsAsync() =>
        _agents.SessionsOf(_workspace).ListAsync(TestContext.Current.CancellationToken);

    private async Task<AgentSession> WaitForSessionAsync(Func<AgentSession, bool> condition)
    {
        for (var i = 0; i < 100; i++)
        {
            if ((await ListSessionsAsync()).SingleOrDefault(condition) is { } session)
            {
                return session;
            }

            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        Assert.Fail($"No recorded session matched: {string.Join(", ", await ListSessionsAsync())}");
        return null!;
    }

    [Fact]
    public void RepositoryFolderOf_NamesTheFoldersRepository()
    {
        var repository = _workspace.Repositories[0];

        Assert.Null(WorkspaceAgents.RepositoryFolderOf(_workspace, _workspace.Folder + Path.DirectorySeparatorChar));
        Assert.Equal("api", WorkspaceAgents.RepositoryFolderOf(_workspace, _workspace.GetWorktreePath(repository)));
        Assert.Equal("gone", WorkspaceAgents.RepositoryFolderOf(_workspace, Path.Combine(_workspace.Folder, "gone")));
    }

    private Workspace CreateWorkspace(string name, bool createMetadataFolder = true)
    {
        var workspace = new Workspace
        {
            Id = Workspace.NewId(),
            Name = name,
            Folder = Path.Combine(_root.FullName, name),
            Repositories = [new WorkspaceRepository("api", "/repos/api", "feature", "origin/main")],
        };
        Directory.CreateDirectory(workspace.Folder);
        if (createMetadataFolder)
        {
            Directory.CreateDirectory(workspace.MetadataFolder);
        }

        return workspace;
    }
}
