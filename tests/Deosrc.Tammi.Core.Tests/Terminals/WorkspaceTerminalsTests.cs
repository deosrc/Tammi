using Tammi.Core.Terminals;
using Tammi.Core.Tests.Platform.Pty;
using Tammi.Core.Tests.Settings;
using Tammi.Core.Workspaces;

namespace Tammi.Core.Tests.Terminals;

public sealed class WorkspaceTerminalsTests : IAsyncDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly RecordingPtyService _pty = new();
    private readonly WorkspaceTerminals _terminals;
    private readonly Workspace _workspace = CreateWorkspace("one");
    private readonly Workspace _other = CreateWorkspace("two");

    public WorkspaceTerminalsTests()
    {
        _terminals = new WorkspaceTerminals(new TerminalLauncher(_pty, new InMemorySettingsStore()));
    }

    public ValueTask DisposeAsync() => _terminals.DisposeAsync();

    [Fact]
    public void Start_ByDefault_StartsInTheWorkspaceFolder()
    {
        var terminal = _terminals.Start(_workspace);

        Assert.Equal(_workspace.Folder, Assert.Single(_pty.Requests).Request.WorkingDirectory);
        Assert.Equal(_workspace.Folder, terminal.WorkingDirectory);
        Assert.Equal(_workspace.Id, terminal.WorkspaceId);
        Assert.Equal("Terminal", terminal.Name);
    }

    [Fact]
    public void Start_InRepository_StartsInItsWorktreeAndIsNamedAfterIt()
    {
        var repository = _workspace.Repositories[0];

        var terminal = _terminals.Start(_workspace, repository);

        Assert.Equal(_workspace.GetWorktreePath(repository), Assert.Single(_pty.Requests).Request.WorkingDirectory);
        Assert.Equal("api", terminal.Name);
    }

    [Fact]
    public void Start_NameInUse_AddsTheFirstFreeNumber()
    {
        _terminals.Start(_workspace);
        _terminals.Start(_workspace);
        _terminals.Start(_workspace);
        _terminals.Start(_other);

        Assert.Equal(["Terminal", "Terminal 2", "Terminal 3"], _terminals.For(_workspace.Id).Select(t => t.Name));
        Assert.Equal("Terminal", Assert.Single(_terminals.For(_other.Id)).Name);
    }

    [Fact]
    public async Task Start_AfterClosing_ReusesTheName()
    {
        var first = _terminals.Start(_workspace);
        _terminals.Start(_workspace);
        await _terminals.CloseAsync(first);

        Assert.Equal("Terminal", _terminals.Start(_workspace).Name);
    }

    [Fact]
    public void For_ListsOnlyThatWorkspacesTerminals_InStartOrder()
    {
        var a = _terminals.Start(_workspace);
        _terminals.Start(_other);
        var b = _terminals.Start(_workspace, _workspace.Repositories[0]);

        Assert.Equal([a, b], _terminals.For(_workspace.Id));
        Assert.Empty(_terminals.For("unknown"));
    }

    [Fact]
    public void Start_RaisesChanged()
    {
        var changed = new List<string>();
        _terminals.Changed += changed.Add;

        _terminals.Start(_workspace);

        Assert.Equal([_workspace.Id], changed);
    }

    [Fact]
    public async Task ShellExits_RaisesChangedAndKeepsTheTerminal()
    {
        var terminal = _terminals.Start(_workspace);
        var exited = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _terminals.Changed += id => exited.TrySetResult(id);

        _pty.Sessions[0].Exit(0);

        Assert.Equal(_workspace.Id, await exited.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken));
        Assert.True(terminal.HasExited);
        Assert.Same(terminal, Assert.Single(_terminals.For(_workspace.Id)));
    }

    [Fact]
    public async Task CloseAsync_EndsTheShellAndRemovesIt()
    {
        var terminal = _terminals.Start(_workspace);
        var changed = new List<string>();
        _terminals.Changed += changed.Add;

        await _terminals.CloseAsync(terminal);
        await _terminals.CloseAsync(terminal);

        Assert.True(_pty.Sessions[0].Disposed);
        Assert.Empty(_terminals.For(_workspace.Id));
        Assert.Contains(_workspace.Id, changed);
    }

    [Fact]
    public async Task CloseAllAsync_EndsOnlyThatWorkspacesTerminals()
    {
        _terminals.Start(_workspace);
        _terminals.Start(_other);
        _terminals.Start(_workspace);

        await _terminals.CloseAllAsync(_workspace.Id);

        Assert.Equal([true, false, true], _pty.Sessions.Select(session => session.Disposed));
        Assert.Empty(_terminals.For(_workspace.Id));
        Assert.Single(_terminals.For(_other.Id));
    }

    [Fact]
    public async Task DisposeAsync_EndsEveryTerminal_AndStopsNewOnes()
    {
        _terminals.Start(_workspace);
        _terminals.Start(_other);

        await _terminals.DisposeAsync();

        Assert.All(_pty.Sessions, session => Assert.True(session.Disposed));
        Assert.Throws<ObjectDisposedException>(() => _terminals.Start(_workspace));
    }

    private static Workspace CreateWorkspace(string name) => new()
    {
        Id = Workspace.NewId(),
        Name = name,
        Folder = Path.Combine(Path.GetTempPath(), "workspaces", name),
        Repositories = [new WorkspaceRepository("api", "/repos/api", "feature", "origin/main")],
    };
}
