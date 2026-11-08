using System.Collections.Concurrent;
using Deosrc.Tammi.Core.Platform;
using Deosrc.Tammi.Core.Settings;
using Deosrc.Tammi.Core.Workspaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Deosrc.Tammi.Core.Agents;

/// <summary>
/// The agents of every workspace. They keep running when a workspace's tab closes, so it can show them again when it
/// reopens, and they all stop when this is disposed, as the app closes.
/// </summary>
/// <remarks>
/// Each agent's Agent Sessions are recorded in its workspace's <c>.tammi/agent-sessions.json</c> (see
/// <see cref="SessionsOf"/>) as soon as they're known, so they can be resumed after a restart.
/// </remarks>
public sealed class WorkspaceAgents(
    AgentLauncher launcher,
    ISettingsStore settingsStore,
    TimeProvider timeProvider,
    ILogger<WorkspaceAgents>? logger = null) : IAsyncDisposable
{
    /// <summary>
    /// The name of an agent whose definition has no name.
    /// </summary>
    public const string DefaultName = "Agent";

    private readonly Lock _lock = new();
    private readonly List<WorkspaceAgent> _agents = [];
    private readonly ILogger _logger = logger ?? NullLogger<WorkspaceAgents>.Instance;
    private readonly ConcurrentDictionary<string, AgentSessionFileStore> _sessionStores =
        new(FileSystemPaths.Comparer);

    private bool _disposed;

    /// <summary>
    /// Raised with a workspace's ID when one of its agents starts, changes state, ends or is closed, or when one of
    /// its Agent Sessions is recorded. It can be raised on a background thread.
    /// </summary>
    public event Action<string>? Changed;

    /// <summary>
    /// Whether the app is closing, and the agents are being closed with it.
    /// </summary>
    public bool IsDisposed
    {
        get
        {
            lock (_lock)
            {
                return _disposed;
            }
        }
    }

    /// <summary>
    /// A workspace's open agents, running or ended, in the order they were started.
    /// </summary>
    public IReadOnlyList<WorkspaceAgent> For(string workspaceId)
    {
        lock (_lock)
        {
            return _agents.Where(agent => agent.WorkspaceId == workspaceId).ToArray();
        }
    }

    /// <summary>
    /// How many agents are running, in every workspace.
    /// </summary>
    public int CountRunning()
    {
        lock (_lock)
        {
            return _agents.Count(agent => !agent.HasExited);
        }
    }

    /// <summary>
    /// The store of a workspace's recorded Agent Sessions, in its <c>.tammi/</c> folder.
    /// </summary>
    public IAgentSessionStore SessionsOf(Workspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var path = Path.Combine(workspace.MetadataFolder, AgentSessionFileStore.DefaultFileName);
        return _sessionStores.GetOrAdd(
            FileSystemPaths.Normalize(path),
            file => new AgentSessionFileStore(file, _logger));
    }

    /// <summary>
    /// Starts an agent in a workspace, in a new Agent Session, and records the session if it can be resumed.
    /// </summary>
    /// <remarks>
    /// An agent that takes an initial prompt is sent the one from the Settings' template, with the path of the
    /// workspace's task context file (see <see cref="TaskContextFile"/>). The file is written first if it doesn't exist
    /// yet, listing the tasks by their keys until their details are read.
    /// </remarks>
    /// <param name="workspace">The workspace.</param>
    /// <param name="definition">The agent to start.</param>
    /// <param name="repository">
    /// The repository to start it in, or <c>null</c> to start it in the Workspace Folder.
    /// </param>
    /// <returns>
    /// The agent, once it has started and its session has been recorded. If the session couldn't be recorded, the
    /// agent still runs, and says why in <see cref="WorkspaceAgent.SessionProblem"/>.
    /// </returns>
    /// <exception cref="FileNotFoundException">The agent's command can't be found.</exception>
    /// <exception cref="DirectoryNotFoundException">The folder to start it in doesn't exist.</exception>
    /// <exception cref="IOException">The task context file couldn't be written.</exception>
    /// <exception cref="ArgumentException">The definition has no command, or its initial prompt can't be passed.</exception>
    /// <exception cref="ObjectDisposedException">The app is closing.</exception>
    public async Task<WorkspaceAgent> StartAsync(
        Workspace workspace,
        AgentDefinition definition,
        WorkspaceRepository? repository = null)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(definition);

        var folder = repository is null ? workspace.Folder : workspace.GetWorktreePath(repository);
        var initialPrompt = definition.AcceptsInitialPrompt
            ? await CreateInitialPromptAsync(workspace).ConfigureAwait(false)
            : null;

        // The terminal resizes the session to fit as soon as it's shown.
        return await AddAsync(
            workspace,
            definition,
            repository?.FolderName,
            () => launcher.Start(definition, folder, PtySize.Default, initialPrompt)).ConfigureAwait(false);
    }

    /// <summary>
    /// Resumes one of a workspace's recorded Agent Sessions, with the Agent Definition that has its agent's name now,
    /// in the folder it was started in. The session is recorded again, as started now.
    /// </summary>
    /// <param name="workspace">The workspace the session belongs to.</param>
    /// <param name="session">The session to resume.</param>
    /// <returns>The agent, once it has started and its session has been recorded.</returns>
    /// <exception cref="InvalidOperationException">
    /// No agent has the session's name now, or the session is already running in one of the workspace's agents.
    /// </exception>
    /// <exception cref="FileNotFoundException">The agent's command can't be found.</exception>
    /// <exception cref="DirectoryNotFoundException">The session's folder doesn't exist any more.</exception>
    /// <exception cref="ArgumentException">
    /// The definition has no command or can't resume sessions, or the session's ID isn't one Tammi could have set.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The app is closing.</exception>
    public Task<WorkspaceAgent> ResumeAsync(Workspace workspace, AgentSession session)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(session);

        var definition = session.FindDefinition(settingsStore.Current.Agents.Definitions)
            ?? throw new InvalidOperationException(
                $"No agent is called \"{session.AgentName}\" now, so the session can't be resumed.");
        if (IsRunning(workspace.Id, session.Id))
        {
            throw new InvalidOperationException("The session is already running.");
        }

        return AddAsync(
            workspace,
            definition,
            RepositoryFolderOf(workspace, session.WorkingDirectory),
            () => launcher.Resume(definition, session, PtySize.Default));
    }

    /// <summary>
    /// Whether an Agent Session is running in one of a workspace's agents.
    /// </summary>
    public bool IsRunning(string workspaceId, string sessionId) =>
        For(workspaceId).Any(agent => !agent.HasExited
            && string.Equals(agent.Agent.SessionId, sessionId, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Stops an agent, and keeps it open (as Exited) until it's closed. Stopping one that has ended does nothing.
    /// </summary>
    /// <returns>Completes once the agent has ended, or been given up on.</returns>
    public Task StopAsync(WorkspaceAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        return agent.Agent.StopAsync();
    }

    /// <summary>
    /// Closes an agent, stopping it if it's running. Closing one that's already closed does nothing.
    /// </summary>
    /// <returns>Completes once the agent has ended, or been given up on.</returns>
    public async Task CloseAsync(WorkspaceAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        lock (_lock)
        {
            if (!_agents.Remove(agent))
            {
                return;
            }
        }

        Changed?.Invoke(agent.WorkspaceId);
        await agent.Agent.StopAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Closes all of a workspace's agents, e.g. before it's deleted.
    /// </summary>
    public Task CloseAllAsync(string workspaceId) =>
        Task.WhenAll(For(workspaceId).Select(CloseAsync));

    /// <summary>
    /// Closes every agent, and stops new ones from starting.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        WorkspaceAgent[] agents;
        lock (_lock)
        {
            _disposed = true;
            agents = [.. _agents];
        }

        await Task.WhenAll(agents.Select(CloseAsync)).ConfigureAwait(false);
    }

    /// <summary>
    /// The folder name of the repository a folder in a workspace is, e.g. where an Agent Session was started.
    /// </summary>
    /// <returns>
    /// The repository's folder name, or <c>null</c> for the Workspace Folder. A folder that's neither (e.g. a
    /// repository that's no longer in the workspace) is named by its last part.
    /// </returns>
    public static string? RepositoryFolderOf(Workspace workspace, string folder)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);

        folder = FileSystemPaths.Normalize(folder);
        if (FileSystemPaths.Comparer.Equals(folder, FileSystemPaths.Normalize(workspace.Folder)))
        {
            return null;
        }

        return workspace.Repositories
            .FirstOrDefault(repository => FileSystemPaths.Comparer.Equals(
                folder,
                FileSystemPaths.Normalize(workspace.GetWorktreePath(repository))))
            ?.FolderName ?? Path.GetFileName(folder);
    }

    private async Task<string> CreateInitialPromptAsync(Workspace workspace)
    {
        string contextFile;
        try
        {
            contextFile = await TaskContextFile.CreateIfMissingAsync(workspace).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"The task context file for agents couldn't be written. {ex.Message}", ex);
        }

        return InitialPrompt.Expand(settingsStore.Current.Agents.InitialPromptTemplate, contextFile);
    }

    private async Task<WorkspaceAgent> AddAsync(
        Workspace workspace,
        AgentDefinition definition,
        string? repositoryFolder,
        Func<Agent> start)
    {
        var agentName = string.IsNullOrWhiteSpace(definition.Name) ? DefaultName : definition.Name.Trim();
        var baseName = repositoryFolder is null ? agentName : $"{agentName} ({repositoryFolder})";
        WorkspaceAgent agent;
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            Agent started;
            try
            {
                started = start();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Couldn't start the agent {Definition}.", agentName);
                throw;
            }

            agent = new WorkspaceAgent(workspace.Id, UniqueName(workspace.Id, baseName), started);
            _agents.Add(agent);
        }

        _logger.LogInformation(
            "Started the agent {Name} (process {ProcessId}) in {Folder}, session {SessionId}.",
            agent.Name,
            agent.Agent.Session.ProcessId,
            agent.Agent.WorkingDirectory,
            agent.Agent.SessionId);
        _ = agent.Agent.Session.Exited.ContinueWith(
            exited => _logger.LogInformation(
                "The agent {Name} exited with {ExitCode}.",
                agent.Name,
                exited.IsCompletedSuccessfully ? exited.Result : null),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        var store = SessionsOf(workspace);
        agent.Agent.StateChanged += _ => Changed?.Invoke(agent.WorkspaceId);

        // Raised when the agent's hooks report its session, or that it has moved to another one.
        agent.Agent.SessionIdChanged += changed => _ = RecordSessionAsync(agent, store);
        Changed?.Invoke(workspace.Id);

        await RecordSessionAsync(agent, store).ConfigureAwait(false);
        return agent;
    }

    private async Task RecordSessionAsync(WorkspaceAgent agent, IAgentSessionStore store)
    {
        if (AgentSession.Of(agent.Agent, timeProvider.GetUtcNow()) is not { } session)
        {
            return;
        }

        try
        {
            await store.RecordAsync(session).ConfigureAwait(false);
            agent.SessionProblem = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Couldn't record the session of the agent {Name}.", agent.Name);
            agent.SessionProblem =
                $"The agent's session couldn't be recorded, so it can't be resumed after a restart. {ex.Message}";
        }

        Changed?.Invoke(agent.WorkspaceId);
    }

    private string UniqueName(string workspaceId, string name)
    {
        var used = _agents
            .Where(agent => agent.WorkspaceId == workspaceId)
            .Select(agent => agent.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!used.Contains(name))
        {
            return name;
        }

        var number = 2;
        while (used.Contains($"{name} {number}"))
        {
            number++;
        }

        return $"{name} {number}";
    }
}
