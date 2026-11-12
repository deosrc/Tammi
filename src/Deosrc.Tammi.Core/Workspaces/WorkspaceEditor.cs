using Deosrc.Tammi.Core.Git;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// Changes an existing workspace: replaces its task list, and adds repositories as new Workspace Worktrees, then
/// writes its <c>.tammi/</c> metadata again.
/// </summary>
/// <remarks>
/// Added repositories are created as <see cref="WorkspaceCreator"/> creates them. Everything is checked first. If a
/// step then fails, or the edit is cancelled, the worktrees and branches it created are removed again, and the metadata
/// (written last) is left as it was. Repositories are never removed from a workspace this way.
/// </remarks>
public sealed class WorkspaceEditor(
    IWorkspaceStore store,
    IGitWorktreeService worktrees,
    ILogger<WorkspaceEditor>? logger = null)
{
    private readonly ILogger _logger = logger ?? NullLogger<WorkspaceEditor>.Instance;
    private readonly WorkspaceWorktreeSteps _steps = new(worktrees, logger ?? NullLogger<WorkspaceEditor>.Instance);

    /// <summary>
    /// Checks a workspace can be edited as requested, without changing anything.
    /// </summary>
    /// <returns>The problems found, or none if it can be edited.</returns>
    /// <exception cref="WorkspaceNotFoundException">The workspace isn't known, or can't be read.</exception>
    public async Task<IReadOnlyList<string>> CheckAsync(
        string workspaceId,
        WorkspaceEditRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = await FindAsync(workspaceId, cancellationToken).ConfigureAwait(false);
        return await CheckAsync(workspace, Tidy(request), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Checks the request (see <see cref="CheckAsync(string, WorkspaceEditRequest, CancellationToken)"/>), then edits the
    /// workspace.
    /// </summary>
    /// <param name="workspaceId">The workspace's <see cref="Workspace.Id"/>.</param>
    /// <param name="request">The changes.</param>
    /// <param name="progress">Told about each step as it starts.</param>
    /// <param name="cancellationToken">
    /// Stops the edit. What was added is removed again, and the method throws <see cref="OperationCanceledException"/>.
    /// </param>
    /// <exception cref="WorkspaceNotFoundException">The workspace isn't known, or can't be read.</exception>
    /// <exception cref="WorkspaceEditException">The request failed its checks, or a step failed.</exception>
    public async Task<WorkspaceCreationResult> EditAsync(
        string workspaceId,
        WorkspaceEditRequest request,
        IProgress<WorkspaceCreationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var workspace = await FindAsync(workspaceId, cancellationToken).ConfigureAwait(false);
        request = Tidy(request);

        var problems = await CheckAsync(workspace, request, cancellationToken).ConfigureAwait(false);
        if (problems.Count > 0)
        {
            throw new WorkspaceEditException(
                $"The workspace '{workspace.Name}' can't be edited: {string.Join(" ", problems)}",
                workspace.Folder,
                problems: problems);
        }

        // A fetch and a worktree for each added repository, and saving the metadata.
        int totalSteps = 1 + (request.AddedRepositories.Count * 2);
        int completedSteps = 0;
        void Report(string message) =>
            progress?.Report(new WorkspaceCreationProgress(message, completedSteps, totalSteps));

        _logger.LogInformation(
            "Editing the workspace {Name} ({Id}): {TaskCount} tasks, adding {RepositoryCount} repositories.",
            workspace.Name,
            workspace.Id,
            request.Tasks.Count,
            request.AddedRepositories.Count);
        var created = new List<CreatedWorktree>();
        var warnings = new List<string>();
        try
        {
            var repositories = new List<WorkspaceRepository>(workspace.Repositories);
            foreach (var repository in request.AddedRepositories)
            {
                repositories.Add(await _steps.AddAsync(
                    workspace.Folder,
                    request.BranchName,
                    repository,
                    created,
                    warnings,
                    Report,
                    () => completedSteps++,
                    () => completedSteps++,
                    cancellationToken).ConfigureAwait(false));
            }

            cancellationToken.ThrowIfCancellationRequested();
            Report("Saving the workspace…");
            var saved = await store.SaveAsync(
                workspace with { Tasks = request.Tasks, Repositories = repositories },
                // Once the metadata is saved the edit is done, so it's no longer rolled back.
                CancellationToken.None).ConfigureAwait(false);
            completedSteps++;
            Report("Saved.");
            _logger.LogInformation("Edited the workspace {Name} ({Id}).", saved.Name, saved.Id);

            return new WorkspaceCreationResult(saved, warnings);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Editing the workspace {Name} was cancelled. Rolling it back.", workspace.Name);
            await _steps.RollBackAsync(created).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex) when (ex is GitException or WorkspaceException or IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Couldn't edit the workspace {Name}. Rolling it back.", workspace.Name);
            var cleanupProblems = await _steps.RollBackAsync(created).ConfigureAwait(false);
            throw new WorkspaceEditException(
                $"Couldn't edit the workspace '{workspace.Name}'. {ex.Message}",
                workspace.Folder,
                cleanupProblems,
                ex);
        }
    }

    private async Task<Workspace> FindAsync(string workspaceId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workspaceId);
        var listing = await store.FindAsync(workspaceId, cancellationToken).ConfigureAwait(false)
            ?? throw new WorkspaceNotFoundException($"The workspace '{workspaceId}' isn't in the list of workspaces.", string.Empty);
        return listing.Workspace
            ?? throw new WorkspaceNotFoundException(
                $"The workspace '{listing.Name}' can't be edited. {listing.Problem}", listing.Folder);
    }

    private async Task<IReadOnlyList<string>> CheckAsync(
        Workspace workspace,
        WorkspaceEditRequest request,
        CancellationToken cancellationToken)
    {
        var problems = new List<string>();
        if (request.AddedRepositories.Count == 0)
        {
            return problems;
        }

        if (!Directory.Exists(workspace.Folder))
        {
            problems.Add($"The Workspace Folder '{workspace.Folder}' doesn't exist.");
            return problems;
        }

        // The default branch is the workspace's own when it has repositories, so the new ones join it, unless they
        // choose their own branch.
        bool usesDefault = request.AddedRepositories.Any(repository => repository.BranchName is null);
        string? ownBranch = workspace.Repositories.Select(repository => repository.Branch).FirstOrDefault();
        if (usesDefault && ownBranch is not null && request.BranchName != ownBranch)
        {
            problems.Add($"The workspace's branch is '{ownBranch}', so added repositories must use it.");
        }

        if (usesDefault && !await worktrees.IsValidBranchNameAsync(request.BranchName, cancellationToken)
            .ConfigureAwait(false))
        {
            problems.Add($"'{request.BranchName}' isn't a valid branch name.");
        }

        await _steps.CheckAsync(
            request.AddedRepositories,
            workspace.Repositories,
            request.BranchName,
            problems,
            workspace.Folder,
            cancellationToken).ConfigureAwait(false);
        return problems;
    }

    private static WorkspaceEditRequest Tidy(WorkspaceEditRequest request) => request with
    {
        BranchName = request.BranchName?.Trim() ?? string.Empty,
        AddedRepositories =
        [
            .. request.AddedRepositories.Select(repository => repository with
            {
                FolderName = repository.FolderName?.Trim() ?? string.Empty,
                MergeTarget = repository.MergeTarget?.Trim() ?? string.Empty,
                BranchName = repository.BranchName?.Trim(),
            }),
        ],
    };
}
