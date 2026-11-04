using TAMMi.Core.Git;
using TAMMi.Core.Platform;

namespace TAMMi.Core.Workspaces;

/// <summary>
/// Deletes workspaces: checks for work that would be lost, removes the Workspace Worktrees and the Workspace Folder,
/// removes the workspace from the index, and then deletes the branches the user picks.
/// </summary>
/// <remarks>
/// <para>
/// A workspace whose metadata can't be read is deleted too. Its worktrees are found by looking at the folders in the
/// Workspace Folder, because worktrees are always created directly in it. A workspace whose folder is missing, or
/// now holds a different workspace, only has its index entry removed: the folder isn't this workspace's to delete.
/// </para>
/// <para>
/// Nothing in the Workspace Folder is deleted unless it was listed in the plan the user confirmed. If a worktree has
/// new changes, or something new appears in the folder, deleting stops and the workspace can be checked again.
/// </para>
/// </remarks>
public sealed class WorkspaceDeleter(IWorkspaceStore store, IGitWorktreeService worktrees, IGitRepositoryReader reader)
{
    /// <summary>
    /// Works out what deleting a workspace would remove, and the work that would be lost.
    /// </summary>
    /// <returns>The plan, or <see langword="null"/> if the workspace isn't in the index.</returns>
    public async Task<WorkspaceDeletionPlan?> PlanAsync(string workspaceId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspaceId);
        if (await store.FindAsync(workspaceId, cancellationToken).ConfigureAwait(false) is not { } listing)
        {
            return null;
        }

        Workspace? workspace;
        try
        {
            workspace = await store.LoadAsync(listing.Folder, cancellationToken).ConfigureAwait(false);
        }
        catch (WorkspaceNotFoundException ex)
        {
            return IndexEntryOnly(listing, ex.Message);
        }
        catch (InvalidWorkspaceMetadataException)
        {
            // Its worktrees are found by looking in the folder instead.
            workspace = null;
        }

        if (workspace is not null && workspace.Id != listing.Id)
        {
            return IndexEntryOnly(
                listing,
                $"The folder '{listing.Folder}' now holds a different workspace, '{workspace.Name}'.");
        }

        return await Task.Run(() => PlanFolder(listing, workspace), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes a workspace as planned, once the user has confirmed any work that would be lost. Worktrees the plan
    /// says need confirming are removed whatever their changes. Others are removed only if they still have none.
    /// </summary>
    /// <returns>
    /// The workspace's branches still in their Local Repositories, to offer for deletion (see
    /// <see cref="DeleteBranchesAsync"/>).
    /// </returns>
    /// <exception cref="WorkspaceDeletionException">
    /// The workspace changed since it was planned, or something couldn't be removed. Make a new plan to try again.
    /// </exception>
    public async Task<IReadOnlyList<WorkspaceBranch>> DeleteAsync(
        WorkspaceDeletionPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.Scope == WorkspaceDeletionScope.Folder)
        {
            await EnsureUnchangedAsync(plan, cancellationToken).ConfigureAwait(false);
            foreach (var worktree in plan.Worktrees)
            {
                await RemoveWorktreeAsync(plan, worktree, cancellationToken).ConfigureAwait(false);
            }

            await Task.Run(() => DeleteFolder(plan), cancellationToken).ConfigureAwait(false);
        }

        await store.RemoveFromIndexAsync(plan.WorkspaceId, cancellationToken).ConfigureAwait(false);
        return await FindBranchesAsync(plan, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes branches from their Local Repositories, whether or not they're merged. A branch that can't be deleted
    /// doesn't stop the others.
    /// </summary>
    public async Task<IReadOnlyList<BranchDeletionResult>> DeleteBranchesAsync(
        IEnumerable<WorkspaceBranch> branches,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(branches);
        var results = new List<BranchDeletionResult>();
        foreach (var branch in branches)
        {
            try
            {
                await worktrees.DeleteBranchAsync(branch.LocalRepositoryPath, branch.Branch, cancellationToken)
                    .ConfigureAwait(false);
                results.Add(new BranchDeletionResult(branch, null));
            }
            catch (GitException ex)
            {
                results.Add(new BranchDeletionResult(branch, ex.Message));
            }
        }

        return results;
    }

    private static WorkspaceDeletionPlan IndexEntryOnly(WorkspaceListing listing, string reason) =>
        new(listing.Id, listing.Name, listing.Folder, WorkspaceDeletionScope.IndexEntryOnly) { Reason = reason };

    private WorkspaceDeletionPlan PlanFolder(WorkspaceListing listing, Workspace? workspace)
    {
        var repositories = (workspace?.Repositories ?? [])
            .ToDictionary(repository => repository.FolderName, FileSystemPaths.Comparer);
        var found = new List<WorktreeToDelete>();
        var other = new List<string>();

        foreach (var entry in ReadFolder(listing.Folder))
        {
            repositories.TryGetValue(entry.Name, out var repository);

            // A link is deleted on its own, without what it points at, so it's never treated as a worktree.
            var check = entry is DirectoryInfo { LinkTarget: null } ? worktrees.CheckWorktree(entry.FullName) : null;
            if (check is { Health: not (WorktreeHealth.NotAWorktree or WorktreeHealth.Missing) })
            {
                found.Add(Describe(check, repository));
            }
            else
            {
                other.Add(entry.FullName);
            }
        }

        // A worktree whose folder is already gone is still unregistered from its Local Repository.
        foreach (var repository in repositories.Values)
        {
            var path = Path.Combine(listing.Folder, repository.FolderName);
            if (!Path.Exists(path))
            {
                found.Add(new WorktreeToDelete(path, WorktreeHealth.Missing, repository.LocalRepositoryPath)
                {
                    Repository = repository,
                });
            }
        }

        return new WorkspaceDeletionPlan(listing.Id, listing.Name, listing.Folder, WorkspaceDeletionScope.Folder)
        {
            MetadataRead = workspace is not null,
            Worktrees = [.. found.OrderBy(worktree => worktree.FolderName, StringComparer.CurrentCultureIgnoreCase)],
            OtherContent = [.. other.OrderBy(Path.GetFileName, StringComparer.CurrentCultureIgnoreCase)],
        };
    }

    private WorktreeToDelete Describe(WorktreeCheck check, WorkspaceRepository? repository)
    {
        // RepositoryPath is known for every worktree health other than Missing and NotAWorktree.
        var worktree = new WorktreeToDelete(check.Path, check.Health, check.RepositoryPath!) { Repository = repository };
        switch (check.Health)
        {
            case WorktreeHealth.Healthy or WorktreeHealth.Moved:
                try
                {
                    return worktree with { Status = reader.ReadStatus(check.Path) };
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    return worktree with { Problem = $"It couldn't be checked for changes. {ex.Message}" };
                }

            case WorktreeHealth.RepositoryMissing:
                return worktree with
                {
                    Problem = $"Its local repository '{check.RepositoryPath}' was moved or deleted, so it can't be "
                        + "checked for changes.",
                };

            default:
                return worktree with
                {
                    Problem = $"Its local repository '{check.RepositoryPath}' has no record of it, so it can't be "
                        + "checked for changes.",
                };
        }
    }

    /// <summary>
    /// Throws if the Workspace Folder no longer holds what was planned, e.g. it was moved, or its metadata was fixed
    /// or broken.
    /// </summary>
    private async Task EnsureUnchangedAsync(WorkspaceDeletionPlan plan, CancellationToken cancellationToken)
    {
        bool holdsWorkspace;
        try
        {
            var workspace = await store.LoadAsync(plan.Folder, cancellationToken).ConfigureAwait(false);
            holdsWorkspace = workspace.Id == plan.WorkspaceId;
            if (!holdsWorkspace)
            {
                throw Changed(plan, $"The folder '{plan.Folder}' now holds a different workspace, '{workspace.Name}'.");
            }
        }
        catch (WorkspaceNotFoundException ex)
        {
            throw Changed(plan, ex.Message);
        }
        catch (InvalidWorkspaceMetadataException)
        {
            holdsWorkspace = false;
        }

        if (holdsWorkspace != plan.MetadataRead)
        {
            throw Changed(plan, "Its metadata changed since it was checked.");
        }
    }

    private async Task RemoveWorktreeAsync(
        WorkspaceDeletionPlan plan,
        WorktreeToDelete worktree,
        CancellationToken cancellationToken)
    {
        try
        {
            // Only the worktrees the user confirmed are forced. A clean one that has changed since is kept.
            await worktrees.RemoveWorktreeAsync(
                worktree.LocalRepositoryPath,
                worktree.Path,
                force: worktree.NeedsConfirmation,
                cancellationToken).ConfigureAwait(false);
        }
        catch (WorktreeChangedException ex)
        {
            throw Changed(plan, $"The worktree '{worktree.FolderName}' has changes it didn't have before.", ex);
        }
        catch (Exception ex) when (ex is GitException or IOException or UnauthorizedAccessException)
        {
            throw new WorkspaceDeletionException(
                $"Couldn't remove the worktree '{worktree.FolderName}' of the workspace '{plan.Name}'. {ex.Message}",
                plan.Folder,
                ex);
        }
    }

    private static void DeleteFolder(WorkspaceDeletionPlan plan)
    {
        // Anything left that wasn't in the plan appeared since, so the user hasn't confirmed deleting it.
        var confirmed = new HashSet<string>(plan.OtherContent, FileSystemPaths.Comparer);
        if (ReadFolder(plan.Folder).FirstOrDefault(entry => !confirmed.Contains(entry.FullName)) is { } added)
        {
            throw Changed(plan, $"'{added.Name}' was added to the workspace folder.");
        }

        try
        {
            // The metadata goes last, so a folder that's only partly deleted is still recognised as the workspace.
            FileSystemDeletion.DeleteFolder(plan.Folder, last: Workspace.MetadataFolderName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new WorkspaceDeletionException(
                $"Couldn't delete the workspace folder '{plan.Folder}'. {ex.Message}",
                plan.Folder,
                ex);
        }
    }

    /// <summary>
    /// Lists the entries in a Workspace Folder other than TAMMi's own <c>.tammi/</c> folder.
    /// </summary>
    private static IEnumerable<FileSystemInfo> ReadFolder(string folder) =>
        new DirectoryInfo(folder).EnumerateFileSystemInfos()
            .Where(entry => !FileSystemPaths.Comparer.Equals(entry.Name, Workspace.MetadataFolderName));

    private async Task<IReadOnlyList<WorkspaceBranch>> FindBranchesAsync(
        WorkspaceDeletionPlan plan,
        CancellationToken cancellationToken)
    {
        // The metadata's branch is the one TAMMi created. A worktree found only in the folder offers its checked-out
        // branch, if it had one.
        var candidates = plan.Worktrees
            .Select(worktree => worktree.Repository is { } repository
                ? new BranchCandidate(worktree.LocalRepositoryPath, repository.Branch, repository.BaseBranch, true)
                : new BranchCandidate(worktree.LocalRepositoryPath, worktree.Status?.Branch, null, false))
            .Where(candidate => candidate.Branch is not null && Directory.Exists(candidate.Repository))
            .DistinctBy(candidate => (FileSystemPaths.Normalize(candidate.Repository), candidate.Branch));

        var branches = new List<WorkspaceBranch>();
        foreach (var candidate in candidates)
        {
            var state = await GetBranchStateAsync(
                candidate.Repository,
                candidate.Branch!,
                candidate.BaseBranch,
                cancellationToken).ConfigureAwait(false);
            if (state.Exists)
            {
                branches.Add(new WorkspaceBranch(
                    candidate.Repository,
                    candidate.Branch!,
                    state,
                    candidate.IsWorkspaceBranch));
            }
        }

        return branches;
    }

    private async Task<BranchState> GetBranchStateAsync(
        string repository,
        string branch,
        string? baseBranch,
        CancellationToken cancellationToken)
    {
        try
        {
            return await worktrees.GetBranchStateAsync(repository, branch, baseBranch, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (GitException) when (baseBranch is not null)
        {
            // The base may be gone, e.g. its remote-tracking branch was pruned. Try the remote's default branch.
            return await GetBranchStateAsync(repository, branch, null, cancellationToken).ConfigureAwait(false);
        }
        catch (GitException)
        {
            // Offered, but not selected by default, since it can't be shown to be safe to delete.
            return new BranchState(true, false, false);
        }
    }

    private static WorkspaceDeletionException Changed(
        WorkspaceDeletionPlan plan,
        string what,
        Exception? innerException = null) =>
        new(
            $"The workspace '{plan.Name}' changed since it was checked, so deleting it stopped. {what} Check it "
                + "again to see what would be lost.",
            plan.Folder,
            innerException);

    private sealed record BranchCandidate(
        string Repository,
        string? Branch,
        string? BaseBranch,
        bool IsWorkspaceBranch);
}
