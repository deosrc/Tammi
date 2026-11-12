using Deosrc.Tammi.Core.Git;
using Microsoft.Extensions.Logging;

namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// The repository steps shared by <see cref="WorkspaceCreator"/> and <see cref="WorkspaceEditor"/>: checking the
/// repositories to add, fetching each and creating its Workspace Worktree, and removing worktrees again.
/// </summary>
internal sealed class WorkspaceWorktreeSteps(IGitWorktreeService worktrees, ILogger logger)
{
    /// <summary>
    /// Checks the repositories to add, adding what's wrong with them to <paramref name="problems"/>.
    /// </summary>
    /// <param name="repositories">The repositories to add.</param>
    /// <param name="existing">The repositories the workspace already has, which the new ones mustn't clash with.</param>
    /// <param name="defaultBranch">
    /// The branch for each repository that doesn't choose its own (see <see cref="WorkspaceCreationRepository.BranchName"/>).
    /// </param>
    /// <param name="problems">Gets the problems found.</param>
    /// <param name="existingFolder">
    /// The Workspace Folder, when it already exists, so a worktree's folder mustn't exist in it yet.
    /// </param>
    public async Task CheckAsync(
        IReadOnlyList<WorkspaceCreationRepository> repositories,
        IReadOnlyList<WorkspaceRepository> existing,
        string defaultBranch,
        List<string> problems,
        string? existingFolder,
        CancellationToken cancellationToken)
    {
        var repositoryPaths = new HashSet<string>(
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (var repository in existing)
        {
            repositoryPaths.Add(Path.TrimEndingDirectorySeparator(Path.GetFullPath(repository.LocalRepositoryPath)));
        }

        for (int i = 0; i < repositories.Count; i++)
        {
            var repository = repositories[i];
            var otherNames = repositories.Where((_, other) => other != i).Select(other => other.FolderName)
                .Concat(existing.Select(other => other.FolderName));
            if (WorktreeFolderNames.Validate(repository.FolderName, otherNames) is { } folderProblem)
            {
                problems.Add($"{repository.FolderName}: {folderProblem}");
            }
            else if (existingFolder is not null
                && (Directory.Exists(Path.Combine(existingFolder, repository.FolderName.Trim()))
                    || File.Exists(Path.Combine(existingFolder, repository.FolderName.Trim()))))
            {
                problems.Add($"{repository.FolderName}: '{Path.Combine(existingFolder, repository.FolderName.Trim())}' "
                    + "already exists. Choose another folder name.");
            }

            string label = repository.FolderName.Trim();
            if (!Path.IsPathFullyQualified(repository.LocalRepositoryPath) || !Directory.Exists(repository.LocalRepositoryPath))
            {
                problems.Add($"{label}: the local repository '{repository.LocalRepositoryPath}' doesn't exist.");
                continue;
            }

            if (!repositoryPaths.Add(Path.TrimEndingDirectorySeparator(Path.GetFullPath(repository.LocalRepositoryPath))))
            {
                problems.Add($"{label}: the local repository '{repository.LocalRepositoryPath}' is in the workspace twice.");
                continue;
            }

            string branchName = repository.GetBranch(defaultBranch);
            bool branchIsValid = await worktrees.IsValidBranchNameAsync(branchName, cancellationToken)
                .ConfigureAwait(false);
            if (repository.BranchName is not null && !branchIsValid)
            {
                // The workspace's own branch is checked by the caller.
                problems.Add(string.IsNullOrWhiteSpace(repository.BranchName)
                    ? $"{label}: enter a branch name."
                    : $"{label}: '{branchName}' isn't a valid branch name.");
            }

            bool baseExists = await worktrees.CommitExistsAsync(
                repository.LocalRepositoryPath, repository.MergeTarget ?? string.Empty, cancellationToken)
                .ConfigureAwait(false);
            if (!baseExists)
            {
                problems.Add(string.IsNullOrWhiteSpace(repository.MergeTarget)
                    ? $"{label}: choose a merge target."
                    : $"{label}: the merge target '{repository.MergeTarget}' doesn't exist.");
            }
        }
    }

    /// <summary>
    /// Fetches a repository, creates its worktree on its branch (new, or an existing one) and checks out its submodules.
    /// </summary>
    /// <param name="created">Gets the worktree as soon as it's started, so it can be rolled back if this fails.</param>
    /// <param name="warnings">Gets a warning if the repository couldn't be fetched.</param>
    /// <param name="fetching">Called when the fetch starts, then when the worktree starts, then the submodules.</param>
    public async Task<WorkspaceRepository> AddAsync(
        string workspaceFolder,
        string defaultBranch,
        WorkspaceCreationRepository repository,
        List<CreatedWorktree> created,
        List<string> warnings,
        Action<string> report,
        Action fetched,
        Action added,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (repository.Remote is { } remote)
        {
            report($"Fetching {repository.FolderName}…");
            try
            {
                await worktrees.FetchAsync(repository.LocalRepositoryPath, remote, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (GitCommandException ex)
            {
                logger.LogWarning(
                    "Couldn't fetch {Remote} for {Repository}, so its branch starts from the last fetched "
                    + "{MergeTarget}.",
                    remote,
                    repository.LocalRepositoryPath,
                    repository.MergeTarget);
                warnings.Add($"{repository.FolderName}: couldn't fetch '{remote}', so the branch starts from "
                    + $"the last fetched '{repository.MergeTarget}'. {ex.Message}");
            }
        }

        fetched();

        string branchName = repository.GetBranch(defaultBranch);
        report($"Creating the worktree for {repository.FolderName}…");
        string worktreePath = Path.Combine(workspaceFolder, repository.FolderName);
        // A branch that exists is checked out as it is. Otherwise it's created: new, or a local branch that tracks the
        // remote's. Only a branch Tammi creates is rolled back.
        bool existsLocally = (await worktrees.GetBranchStateAsync(
            repository.LocalRepositoryPath, branchName, cancellationToken: cancellationToken)
            .ConfigureAwait(false)).Exists;
        bool existsOnRemote = !existsLocally
            && repository.Remote is not null
            && await worktrees.CommitExistsAsync(
                repository.LocalRepositoryPath, $"refs/remotes/{repository.Remote}/{branchName}", cancellationToken)
                .ConfigureAwait(false);
        bool createsBranch = !existsLocally;
        created.Add(new CreatedWorktree(repository.LocalRepositoryPath, worktreePath, branchName, createsBranch));
        await worktrees.CreateWorktreeAsync(
            new CreateWorktreeRequest
            {
                RepositoryPath = repository.LocalRepositoryPath,
                WorktreePath = worktreePath,
                BranchName = branchName,
                UseExistingBranch = existsLocally || existsOnRemote,
                Remote = repository.Remote ?? GitWorktreeService.DefaultRemote,
                BaseBranch = repository.MergeTarget,
                Fetch = false,
                InitSubmodules = false,
            },
            cancellationToken).ConfigureAwait(false);

        report($"Checking out the submodules of {repository.FolderName}…");
        await worktrees.InitSubmodulesAsync(worktreePath, cancellationToken).ConfigureAwait(false);
        added();

        return new WorkspaceRepository(
            repository.FolderName,
            repository.LocalRepositoryPath,
            branchName,
            repository.MergeTarget);
    }

    /// <summary>
    /// Removes worktrees and the branches created for them, in the reverse order they were created. Existing branches
    /// are kept.
    /// </summary>
    /// <returns>What couldn't be removed.</returns>
    public async Task<List<string>> RollBackAsync(IReadOnlyList<CreatedWorktree> created)
    {
        var problems = new List<string>();
        for (int i = created.Count - 1; i >= 0; i--)
        {
            var (repositoryPath, worktreePath, branchName, createdBranch) = created[i];
            try
            {
                // Git may have registered the worktree, or created the branch, before failing. A folder git didn't
                // make a worktree of is deleted with the Workspace Folder.
                if (worktrees.CheckWorktree(worktreePath).Health is not (WorktreeHealth.Missing or WorktreeHealth.NotAWorktree))
                {
                    await worktrees.RemoveWorktreeAsync(repositoryPath, worktreePath, force: true).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is GitException or IOException or UnauthorizedAccessException)
            {
                problems.Add($"Couldn't remove the worktree '{worktreePath}'. {ex.Message}");
            }

            if (!createdBranch)
            {
                continue;
            }

            try
            {
                var branch = await worktrees.GetBranchStateAsync(repositoryPath, branchName).ConfigureAwait(false);
                if (branch.Exists)
                {
                    await worktrees.DeleteBranchAsync(repositoryPath, branchName).ConfigureAwait(false);
                }
            }
            catch (GitException ex)
            {
                problems.Add($"Couldn't delete the branch '{branchName}' from '{repositoryPath}'. {ex.Message}");
            }
        }

        return problems;
    }
}
