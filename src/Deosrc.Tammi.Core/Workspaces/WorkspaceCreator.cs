using Deosrc.Tammi.Core.Git;
using Deosrc.Tammi.Core.Platform;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
public sealed class WorkspaceCreator(
    IWorkspaceStore store,
    IGitWorktreeService worktrees,
    ILogger<WorkspaceCreator>? logger = null)
{
    private readonly ILogger _logger = logger ?? NullLogger<WorkspaceCreator>.Instance;
    private readonly WorkspaceWorktreeSteps _steps = new(worktrees, logger ?? NullLogger<WorkspaceCreator>.Instance);

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
        else if (WorkspaceFolderNames.Validate(Path.GetFileName(Path.TrimEndingDirectorySeparator(request.Folder)))
            is { } folderNameProblem)
        {
            problems.Add(folderNameProblem);
        }
        else if (Directory.Exists(request.Folder) || File.Exists(request.Folder))
        {
            problems.Add($"'{request.Folder}' already exists. Choose another folder name or parent folder.");
        }

        // Only needed if a repository uses it, or the workspace has no repositories to check it against.
        if ((request.Repositories.Count == 0 || request.Repositories.Any(repository => repository.BranchName is null))
            && !await worktrees.IsValidBranchNameAsync(request.BranchName ?? string.Empty, cancellationToken)
                .ConfigureAwait(false))
        {
            problems.Add($"'{request.BranchName}' isn't a valid branch name.");
        }

        await _steps.CheckAsync(
            request.Repositories, [], request.BranchName ?? string.Empty, problems, null, cancellationToken)
            .ConfigureAwait(false);

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

        _logger.LogInformation(
            "Creating the workspace {Name} in {Folder} with {RepositoryCount} repositories on the branch {Branch}.",
            request.Name,
            request.Folder,
            request.Repositories.Count,
            request.BranchName);
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
                repositories.Add(await _steps.AddAsync(
                    request.Folder,
                    request.BranchName,
                    repository,
                    created.Worktrees,
                    warnings,
                    Report,
                    () => completedSteps++,
                    () => completedSteps++,
                    cancellationToken).ConfigureAwait(false));
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
            _logger.LogInformation("Created the workspace {Name} ({Id}).", workspace.Name, workspace.Id);

            return new WorkspaceCreationResult(workspace, warnings);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Creating the workspace {Name} was cancelled. Rolling it back.", request.Name);
            await RollBackAsync(created).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex) when (ex is GitException or WorkspaceException or IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Couldn't create the workspace {Name}. Rolling it back.", request.Name);
            var cleanupProblems = await RollBackAsync(created).ConfigureAwait(false);
            foreach (string problem in cleanupProblems)
            {
                _logger.LogWarning("Rolling back the workspace {Name}: {Problem}", request.Name, problem);
            }

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
    private async Task<IReadOnlyList<string>> RollBackAsync(CreatedItems created)
    {
        var problems = await _steps.RollBackAsync(created.Worktrees).ConfigureAwait(false);

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
                MergeTarget = repository.MergeTarget?.Trim() ?? string.Empty,
                BranchName = repository.BranchName?.Trim(),
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
        /// The worktrees started, including one that failed part way.
        /// </summary>
        public List<CreatedWorktree> Worktrees { get; } = [];

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
