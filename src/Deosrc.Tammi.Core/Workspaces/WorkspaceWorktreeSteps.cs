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
    /// <param name="branchName">The branch to create in each repository.</param>
    /// <param name="branchIsValid">Whether git accepts <paramref name="branchName"/>.</param>
    /// <param name="problems">Gets the problems found.</param>
    /// <param name="existingFolder">
    /// The Workspace Folder, when it already exists, so a worktree's folder mustn't exist in it yet.
    /// </param>
    public async Task CheckAsync(
        IReadOnlyList<WorkspaceCreationRepository> repositories,
        IReadOnlyList<WorkspaceRepository> existing,
        string branchName,
        bool branchIsValid,
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

            if (branchIsValid)
            {
                var branch = await worktrees.GetBranchStateAsync(
                    repository.LocalRepositoryPath, branchName, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                if (branch.Exists)
                {
                    problems.Add($"{label}: the branch '{branchName}' already exists. Choose another branch name.");
                }
            }

            bool baseExists = await worktrees.CommitExistsAsync(
                repository.LocalRepositoryPath, repository.BaseBranch ?? string.Empty, cancellationToken)
                .ConfigureAwait(false);
            if (!baseExists)
            {
                problems.Add(string.IsNullOrWhiteSpace(repository.BaseBranch)
                    ? $"{label}: choose a base branch."
                    : $"{label}: the base branch '{repository.BaseBranch}' doesn't exist.");
            }
        }
    }

    /// <summary>
    /// Fetches a repository, creates its worktree on the new branch and checks out its submodules.
    /// </summary>
    /// <param name="created">Gets the worktree as soon as it's started, so it can be rolled back if this fails.</param>
    /// <param name="warnings">Gets a warning if the repository couldn't be fetched.</param>
    /// <param name="fetching">Called when the fetch starts, then when the worktree starts, then the submodules.</param>
    public async Task<WorkspaceRepository> AddAsync(
        string workspaceFolder,
        string branchName,
        WorkspaceCreationRepository repository,
        List<(string RepositoryPath, string WorktreePath)> created,
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
                    + "{BaseBranch}.",
                    remote,
                    repository.LocalRepositoryPath,
                    repository.BaseBranch);
                warnings.Add($"{repository.FolderName}: couldn't fetch '{remote}', so the branch starts from "
                    + $"the last fetched '{repository.BaseBranch}'. {ex.Message}");
            }
        }

        fetched();

        report($"Creating the worktree for {repository.FolderName}…");
        string worktreePath = Path.Combine(workspaceFolder, repository.FolderName);
        created.Add((repository.LocalRepositoryPath, worktreePath));
        await worktrees.CreateWorktreeAsync(
            new CreateWorktreeRequest
            {
                RepositoryPath = repository.LocalRepositoryPath,
                WorktreePath = worktreePath,
                BranchName = branchName,
                BaseBranch = repository.BaseBranch,
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
            repository.BaseBranch);
    }

    /// <summary>
    /// Removes worktrees and their new branches, in the reverse order they were created.
    /// </summary>
    /// <returns>What couldn't be removed.</returns>
    public async Task<List<string>> RollBackAsync(
        IReadOnlyList<(string RepositoryPath, string WorktreePath)> created,
        string branchName)
    {
        var problems = new List<string>();
        for (int i = created.Count - 1; i >= 0; i--)
        {
            var (repositoryPath, worktreePath) = created[i];
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
