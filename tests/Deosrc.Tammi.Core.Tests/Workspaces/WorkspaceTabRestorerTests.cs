using Microsoft.Extensions.Time.Testing;
using Deosrc.Tammi.Core.Agents;
using Deosrc.Tammi.Core.Git;
using Deosrc.Tammi.Core.Tests.Platform.Pty;
using Deosrc.Tammi.Core.Tests.Settings;
using Deosrc.Tammi.Core.Settings;
using Deosrc.Tammi.Core.Workspaces;

namespace Deosrc.Tammi.Core.Tests.Workspaces;

public sealed class WorkspaceTabRestorerTests : IAsyncDisposable
{
    private static readonly AgentDefinition Resumable = new("Resumable", "resumable-agent", "-a")
    {
        SessionIdArguments = ["--id", AgentDefinition.SessionIdPlaceholder],
        ResumeArguments = ["--resume", AgentDefinition.SessionIdPlaceholder],
    };

    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("tammi-tests-");
    private readonly RecordingPtyService _pty = new();
    private readonly InMemorySettingsStore _settings = new(new AppSettings
    {
        Agents = new AgentSettings { CustomDefinitions = [Resumable] },
    });

    private readonly Workspace _workspace;

    public WorkspaceTabRestorerTests()
    {
        _workspace = new Workspace
        {
            Id = Workspace.NewId(),
            Name = "one",
            Folder = Path.Combine(_root.FullName, "one"),
            Repositories = [new WorkspaceRepository("api", "/repos/api", "feature", "origin/main")],
        };
        Directory.CreateDirectory(_workspace.MetadataFolder);
    }

    public ValueTask DisposeAsync()
    {
        _root.Delete(recursive: true);
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task OpenTabs_AreOpenedAgainByANewRestorer()
    {
        var firstRun = NewRun();
        Assert.Empty(await firstRun.Restorer.RestoreAsync(_workspace));
        await firstRun.Agents.StartAsync(_workspace, Resumable);
        firstRun.Graphs.Open(_workspace, _workspace.Repositories[0]);
        await WaitForSavedTabsAsync(2);
        await firstRun.DisposeAsync();

        var secondRun = NewRun();
        Assert.Empty(await secondRun.Restorer.RestoreAsync(_workspace));

        Assert.Single(secondRun.Agents.For(_workspace.Id));
        Assert.Equal("Git: api", Assert.Single(secondRun.Graphs.For(_workspace.Id)).Name);
        await secondRun.DisposeAsync();
    }

    [Fact]
    public async Task RestoreAsync_OnlyTheFirstTimeForAWorkspace()
    {
        var run = NewRun();
        await run.Restorer.RestoreAsync(_workspace);
        await run.Agents.StartAsync(_workspace, Resumable);
        await WaitForSavedTabsAsync(1);

        await run.Restorer.RestoreAsync(_workspace);

        Assert.Single(run.Agents.For(_workspace.Id));
        await run.DisposeAsync();
    }

    [Fact]
    public async Task ClosedTabs_AreNotOpenedAgain()
    {
        var firstRun = NewRun();
        await firstRun.Restorer.RestoreAsync(_workspace);
        var agent = await firstRun.Agents.StartAsync(_workspace, Resumable);
        var graph = firstRun.Graphs.Open(_workspace, _workspace.Repositories[0]);
        await WaitForSavedTabsAsync(2);
        await firstRun.Agents.CloseAsync(agent);
        firstRun.Graphs.Close(graph);
        await WaitForSavedTabsAsync(0);
        await firstRun.DisposeAsync();

        var secondRun = NewRun();
        await secondRun.Restorer.RestoreAsync(_workspace);

        Assert.Empty(secondRun.Agents.For(_workspace.Id));
        Assert.Empty(secondRun.Graphs.For(_workspace.Id));
        await secondRun.DisposeAsync();
    }

    [Fact]
    public async Task RestoreAsync_RepositoryNoLongerInTheWorkspace_ReportsAProblem()
    {
        await new SavedTabsFileStore(Path.Combine(_workspace.MetadataFolder, SavedTabsFileStore.DefaultFileName))
            .SetAsync([new SavedTab(SavedTab.GitGraphKind, Repository: "gone")], TestContext.Current.CancellationToken);
        var run = NewRun();

        var problems = await run.Restorer.RestoreAsync(_workspace);

        Assert.Contains("gone", Assert.Single(problems));
        Assert.Empty(run.Graphs.For(_workspace.Id));
        await run.DisposeAsync();
    }

    private async Task WaitForSavedTabsAsync(int count)
    {
        var store = new SavedTabsFileStore(Path.Combine(_workspace.MetadataFolder, SavedTabsFileStore.DefaultFileName));
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while ((await store.ListAsync(TestContext.Current.CancellationToken)).Count != count)
        {
            Assert.True(DateTime.UtcNow < deadline, $"Expected {count} saved tabs.");
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }
    }

    private Run NewRun()
    {
        var agents = new WorkspaceAgents(
            new AgentLauncher(_pty),
            _settings,
            new FakeTimeProvider(new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero)));
        var graphs = new WorkspaceGitGraphs(new GitRepositoryReader(), _settings);
        return new Run(agents, graphs, new WorkspaceTabRestorer(agents, graphs));
    }

    private sealed record Run(WorkspaceAgents Agents, WorkspaceGitGraphs Graphs, WorkspaceTabRestorer Restorer)
        : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await Agents.DisposeAsync();
            Graphs.Dispose();
            Restorer.Dispose();
        }
    }
}
