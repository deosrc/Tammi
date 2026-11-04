using Microsoft.Extensions.Time.Testing;
using TAMMi.Core.Agents;
using TAMMi.Core.Settings;
using TAMMi.Core.Terminals;
using TAMMi.Core.Tests.Platform.Pty;
using TAMMi.Core.Tests.Settings;
using TAMMi.Core.Workspaces;

namespace TAMMi.Core.Tests.Agents;

public sealed class WorkspaceAgentsTests : IAsyncDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static readonly AgentDefinition Resumable = new("Resumable", "resumable-agent", "-a")
    {
        SessionIdArguments = ["--id", AgentDefinition.SessionIdPlaceholder],
        ResumeArguments = ["--resume", AgentDefinition.SessionIdPlaceholder],
    };

    private static readonly AgentDefinition Plain = new("Plain", "plain-agent");

    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("tammi-tests-");
    private readonly RecordingPtyService _pty = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero));
    private readonly InMemorySettingsStore _settings = new(new AppSettings
    {
        Agents = new AgentSettings { CustomDefinitions = [Resumable, Plain] },
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

        var request = _pty.Requests[1].Request;
        Assert.Equal(["-a", "--resume", session.Id], request.Arguments);
        Assert.Equal(_workspace.GetWorktreePath(repository), request.WorkingDirectory);
        Assert.Equal(session.Id, resumed.Agent.SessionId);
        Assert.Equal("Resumable (api) 2", resumed.Name);
        var recorded = Assert.Single(await _agents.SessionsOf(_workspace).ListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(_time.GetUtcNow(), recorded.LastStartedAt);
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
