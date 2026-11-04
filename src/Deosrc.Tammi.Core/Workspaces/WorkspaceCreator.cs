using Deosrc.Tammi.Core.Git;
using Deosrc.Tammi.Core.Platform;

namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// Creates a workspace on disk: the Workspace Folder, a Workspace Worktree of each repository on a new branch (with its
/// submodules), and the <c>.tammi/</c> metadata, then adds it to the index so it's listed on Home.
/// </summary>
/// <remarks>
/// Everything is checked before anything is created. If a step then fails, or creation is cancelled, everything
/// created so far is removed again: the worktrees, their new branches, the Workspace Folder and any parent folders it
/// created. The metadata is written last, so a workspace that's only partly created is never listed.
/// </remarks>
public sealed class WorkspaceCreator(IWorkspaceStore store, IGitWorktreeService worktrees)
{
    /// <summary>
    /// Checks a workspace can be created as requested, without changing anything.
    /// </summary>
    /// <returns>The problems found, or none if it can be created.</returns>
    public async Task<IReadOnlyList<string>> CheckAsync(
        WorkspaceCreationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var problems = new List<string>();

        if (WorkspaceNames.Validate(request.Name) is { } nameProblem)
        {
            problems.Add(nameProblem);
        }

        if (string.IsNullOrWhiteSpace(request.Folder) || !Path.IsPathFullyQualified(request.Folder))
        {
            problems.Add($"The Workspace Folder '{request.Folder}' isn't a full path.");
        }
        else if (Directory.Exists(request.Folder) || File.Exists(request.Folder))
        {
            problems.Add($"'{request.Folder}' already exists. Choose another name or parent folder.");
        }

        bool branchIsValid = await worktrees.IsValidBranchNameAsync(request.BranchName ?? string.Empty, cancellationToken)
            .ConfigureAwait(false);
        if (!branchIsValid)
        {
            problems.Add($"'{request.BranchName}' isn't a valid branch name.");
        }

        var repositoryPaths = new HashSet<string>(
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        for (int i = 0; i < request.Repositories.Count; i++)
        {
            var repository = request.Repositories[i];
            var otherNames = request.Repositories.Where((_, other) => other != i).Select(other => other.FolderName);
            if (WorktreeFolderNames.Validate(repository.FolderName, otherNames) is { } folderProblem)
            {
                problems.Add($"{repository.FolderName}: {folderProblem}");
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
                    repository.LocalRepositoryPath, request.BranchName!, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                if (branch.Exists)
                {
                    problems.Add($"{label}: the branch '{request.BranchName}' already exists. Choose another branch name.");
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

        return problems;
    }

    /// <summary>
    /// Checks the request (see <see cref="CheckAsync"/>), then creates the workspace.
    /// </summary>
    /// <param name="request">The workspace to create.</param>
    /// <param name="progress">Told about each step as it starts.</param>
    /// <param name="cancellationToken">
    /// Stops creating the workspace. What was created is removed again, and the method throws
    /// <see cref="OperationCanceledException"/>.
    /// </param>
    /// <exception cref="WorkspaceCreationException">
    /// The request failed its checks, or a step failed. Nothing is left on disk unless it's listed in
    /// <see cref="WorkspaceCreationException.CleanupProblems"/>.
    /// </exception>
    public async Task<WorkspaceCreationResult> CreateAsync(
        WorkspaceCreationRequest request,
        IProgress<WorkspaceCreationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        request = Tidy(request);

        var problems = await CheckAsync(request, cancellationToken).ConfigureAwait(false);
        if (problems.Count > 0)
        {
            throw new WorkspaceCreationException(
                $"The workspace '{request.Name}' can't be created: {string.Join(" ", problems)}",
                request.Folder,
                problems: problems);
        }

        // Creating the folder, a fetch and a worktree for each repository, and saving the metadata.
        int totalSteps = 2 + (request.Repositories.Count * 2);
        int completedSteps = 0;
        void Report(string message) =>
            progress?.Report(new WorkspaceCreationProgress(message, completedSteps, totalSteps));

        var created = new CreatedItems(request.Folder);
        var warnings = new List<string>();
        try
        {
            Report("Creating the Workspace Folder…");
            created.CreateFolder();
            completedSteps++;

            var repositories = new List<WorkspaceRepository>();
            foreach (var repository in request.Repositories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (repository.Remote is { } remote)
                {
                    Report($"Fetching {repository.FolderName}…");
                    try
                    {
                        await worktrees.FetchAsync(repository.LocalRepositoryPath, remote, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (GitCommandException ex)
                    {
                        warnings.Add($"{repository.FolderName}: couldn't fetch '{remote}', so the branch starts from "
                            + $"the last fetched '{repository.BaseBranch}'. {ex.Message}");
                    }
                }

                completedSteps++;

                Report($"Creating the worktree for {repository.FolderName}…");
                string worktreePath = Path.Combine(request.Folder, repository.FolderName);
                created.Worktrees.Add((repository.LocalRepositoryPath, worktreePath));
                await worktrees.CreateWorktreeAsync(
                    new CreateWorktreeRequest
                    {
                        RepositoryPath = repository.LocalRepositoryPath,
                        WorktreePath = worktreePath,
                        BranchName = request.BranchName,
                        BaseBranch = repository.BaseBranch,
                        Fetch = false,
                        InitSubmodules = false,
                    },
                    cancellationToken).ConfigureAwait(false);

                Report($"Checking out the submodules of {repository.FolderName}…");
                await worktrees.InitSubmodulesAsync(worktreePath, cancellationToken).ConfigureAwait(false);
                completedSteps++;

                repositories.Add(new WorkspaceRepository(
                    repository.FolderName,
                    repository.LocalRepositoryPath,
                    request.BranchName,
                    repository.BaseBranch));
            }

            cancellationToken.ThrowIfCancellationRequested();
            Report("Saving the workspace…");
            var workspace = await store.SaveAsync(
                new Workspace
                {
                    Id = Workspace.NewId(),
                    Name = request.Name,
                    Folder = request.Folder,
                    Tasks = request.Tasks,
                    Repositories = repositories,
                },
                // Once the workspace is saved it's listed, so it's no longer rolled back.
                CancellationToken.None).ConfigureAwait(false);
            completedSteps++;
            Report("Created.");

            return new WorkspaceCreationResult(workspace, warnings);
        }
        catch (OperationCanceledException)
        {
            await RollBackAsync(created, request.BranchName).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex) when (ex is GitException or WorkspaceException or IOException or UnauthorizedAccessException)
        {
            var cleanupProblems = await RollBackAsync(created, request.BranchName).ConfigureAwait(false);
            throw new WorkspaceCreationException(
                $"Couldn't create the workspace '{request.Name}'. {ex.Message}",
                request.Folder,
                cleanupProblems,
                ex);
        }
    }

    /// <summary>
    /// Removes what was created for a workspace that failed, in the reverse order it was created.
    /// </summary>
    /// <returns>What couldn't be removed.</returns>
    private async Task<IReadOnlyList<string>> RollBackAsync(CreatedItems created, string branchName)
    {
        var problems = new List<string>();
        for (int i = created.Worktrees.Count - 1; i >= 0; i--)
        {
            var (repositoryPath, worktreePath) = created.Worktrees[i];
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

        try
        {
            created.DeleteFolders();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            problems.Add($"Couldn't delete the folder '{created.Folder}'. {ex.Message}");
        }

        return problems;
    }

    private static WorkspaceCreationRequest Tidy(WorkspaceCreationRequest request) => request with
    {
        Name = request.Name?.Trim() ?? string.Empty,
        Folder = string.IsNullOrWhiteSpace(request.Folder) || !Path.IsPathFullyQualified(request.Folder)
            ? request.Folder ?? string.Empty
            : Path.TrimEndingDirectorySeparator(Path.GetFullPath(request.Folder)),
        BranchName = request.BranchName?.Trim() ?? string.Empty,
        Repositories =
        [
            .. request.Repositories.Select(repository => repository with
            {
                FolderName = repository.FolderName?.Trim() ?? string.Empty,
                BaseBranch = repository.BaseBranch?.Trim() ?? string.Empty,
            }),
        ],
    };

    /// <summary>
    /// What's been created so far, to remove if creation fails.
    /// </summary>
    private sealed class CreatedItems(string folder)
    {
        /// <summary>
        /// The parent folders that didn't exist, from the outermost in.
        /// </summary>
        private readonly List<string> _createdParents = [];

        private bool _createdFolder;

        public string Folder { get; } = folder;

        /// <summary>
        /// The worktrees started, as (Local Repository, worktree folder), including one that failed part way.
        /// </summary>
        public List<(string RepositoryPath, string WorktreePath)> Worktrees { get; } = [];

        public void CreateFolder()
        {
            for (string? parent = Path.GetDirectoryName(Folder);
                 parent is not null && !Directory.Exists(parent);
                 parent = Path.GetDirectoryName(parent))
            {
                _createdParents.Insert(0, parent);
            }

            // It was checked before, but something else may have created it since, and it isn't Tammi's to delete.
            if (Directory.Exists(Folder) || File.Exists(Folder))
            {
                throw new IOException($"'{Folder}' already exists. Choose another name or parent folder.");
            }

            Directory.CreateDirectory(Folder);
            _createdFolder = true;
        }

        public void DeleteFolders()
        {
            if (_createdFolder)
            {
                FileSystemDeletion.DeleteFolder(Folder);
            }

            // Only folders that are still empty, in case something else was put in them meanwhile.
            for (int i = _createdParents.Count - 1; i >= 0; i--)
            {
                string parent = _createdParents[i];
                if (!Directory.Exists(parent) || Directory.EnumerateFileSystemEntries(parent).Any())
                {
                    break;
                }

                Directory.Delete(parent);
            }
        }
    }
}
