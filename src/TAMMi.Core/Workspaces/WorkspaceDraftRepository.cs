using TAMMi.Core.Catalogue;
using TAMMi.Core.Git;
using TAMMi.Core.Integrations;

namespace TAMMi.Core.Workspaces;

/// <summary>
/// A repository chosen in the Create Workspace flow, with its worktree's folder name and base branch.
/// </summary>
/// <remarks>
/// The folder name follows the suggestion (see <see cref="WorktreeFolderNames.Suggest"/>) until the user edits it, like
/// the workspace's name. The base branch is the remote's default branch, found by <see cref="LoadAsync"/>, until the
/// user chooses another.
/// </remarks>
public sealed class WorkspaceDraftRepository
{
    private readonly WorkspaceDraft _draft;
    private string _folderName = string.Empty;
    private string _baseBranch = string.Empty;
    private readonly HashSet<TaskReference> _chosenForTasks = [];

    internal WorkspaceDraftRepository(WorkspaceDraft draft, CatalogueRepository repository)
    {
        _draft = draft;
        Repository = repository;
        Remote = ChooseRemote(repository);
        Owner = GitRemoteUrls.GetOwner(Remote?.Url)
            ?? Path.GetFileName(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(repository.Path)));
    }

    /// <summary>
    /// The Local Repository.
    /// </summary>
    public CatalogueRepository Repository { get; }

    /// <summary>
    /// The remote that's fetched, and whose default branch is the base: <c>origin</c>, or the first remote if there's
    /// no <c>origin</c>. <see langword="null"/> if the repository has no remotes.
    /// </summary>
    public GitRemote? Remote { get; }

    /// <summary>
    /// The tasks this repository was chosen for automatically, because they belong to it. Empty if the user chose it.
    /// </summary>
    public IReadOnlyCollection<TaskReference> ChosenForTasks => _chosenForTasks;

    /// <summary>
    /// The owner put in front of the folder name when it clashes: from <see cref="Remote"/>'s URL, or the name of the
    /// folder the Local Repository is in.
    /// </summary>
    public string? Owner { get; }

    /// <summary>
    /// The worktree's folder name, as entered or suggested. Setting it to anything but blank stops it following the
    /// suggestion, and clearing it makes it follow again the next time the repositories change.
    /// </summary>
    public string FolderName
    {
        get => _folderName;
        set
        {
            _folderName = value ?? string.Empty;
            IsFolderNameEdited = !string.IsNullOrWhiteSpace(_folderName);
            _draft.FollowFolderNameSuggestions(except: this);
        }
    }

    /// <summary>
    /// Whether the user entered the <see cref="FolderName"/>.
    /// </summary>
    public bool IsFolderNameEdited { get; private set; }

    /// <summary>
    /// Why the <see cref="FolderName"/> can't be used, or <see langword="null"/> if it can.
    /// </summary>
    public string? FolderNameProblem => WorktreeFolderNames.Validate(
        _folderName,
        _draft.Repositories.Where(other => other != this).Select(other => other.FolderName));

    /// <summary>
    /// What the workspace's branch starts from, e.g. <c>origin/main</c>. Empty until <see cref="LoadAsync"/> finds the
    /// remote's default branch, or if it can't.
    /// </summary>
    public string BaseBranch
    {
        get => _baseBranch;
        set
        {
            _baseBranch = value ?? string.Empty;
            IsBaseBranchEdited = true;
        }
    }

    /// <summary>
    /// Whether the user chose the <see cref="BaseBranch"/>.
    /// </summary>
    public bool IsBaseBranchEdited { get; private set; }

    /// <summary>
    /// The remote's default branch, e.g. <c>origin/main</c>, once it's found.
    /// </summary>
    public string? DefaultBranch { get; private set; }

    /// <summary>
    /// The branches the base can be chosen from, once they're loaded.
    /// </summary>
    public IReadOnlyList<string> Branches { get; private set; } = [];

    /// <summary>
    /// Whether <see cref="LoadAsync"/> is running.
    /// </summary>
    public bool IsLoading { get; private set; }

    /// <summary>
    /// Whether <see cref="LoadAsync"/> has finished, whether or not it found the default branch.
    /// </summary>
    public bool IsLoaded { get; private set; }

    /// <summary>
    /// Why the remote couldn't be fetched, or <see langword="null"/>. The branches fetched before are still offered.
    /// </summary>
    public string? FetchProblem { get; private set; }

    /// <summary>
    /// Why the remote's default branch couldn't be found, or <see langword="null"/>.
    /// </summary>
    public string? DefaultBranchProblem { get; private set; }

    /// <summary>
    /// Fetches <see cref="Remote"/>, then finds its default branch, which becomes the <see cref="BaseBranch"/> unless
    /// the user has chosen one, and the branches to choose from.
    /// </summary>
    public async Task LoadAsync(IGitWorktreeService worktrees, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(worktrees);
        // The continuations stay on the caller's context (the UI's), which reads this state as it renders.
        IsLoading = true;
        try
        {
            if (Remote is { } remote)
            {
                try
                {
                    await worktrees.FetchAsync(Repository.Path, remote.Name, cancellationToken).ConfigureAwait(true);
                    FetchProblem = null;
                }
                catch (GitException ex)
                {
                    FetchProblem = $"Couldn't fetch '{remote.Name}'. {ex.Message}";
                }

                try
                {
                    DefaultBranch = await worktrees.GetDefaultBranchAsync(Repository.Path, remote.Name, cancellationToken)
                        .ConfigureAwait(true);
                    DefaultBranchProblem = null;
                }
                catch (GitException ex)
                {
                    DefaultBranchProblem = ex.Message;
                }
            }
            else
            {
                DefaultBranchProblem = "The repository has no remotes, so choose the branch to start from.";
            }

            try
            {
                Branches = await worktrees.ListBranchesAsync(Repository.Path, cancellationToken).ConfigureAwait(true);
            }
            catch (GitException)
            {
                // Only offered as suggestions, so the user can still enter a branch.
                Branches = [];
            }

            if (!IsBaseBranchEdited && DefaultBranch is not null)
            {
                _baseBranch = DefaultBranch;
            }

            IsLoaded = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    internal void AddChosenForTask(TaskReference task) => _chosenForTasks.Add(task);

    /// <summary>
    /// The remote a repository is fetched from: <c>origin</c>, or its first remote if there's no <c>origin</c>.
    /// </summary>
    internal static GitRemote? ChooseRemote(CatalogueRepository repository) =>
        repository.Remotes.FirstOrDefault(remote => remote.Name == GitWorktreeService.DefaultRemote)
            ?? repository.Remotes.FirstOrDefault();

    /// <summary>
    /// Sets the suggested folder name, unless the user entered one.
    /// </summary>
    internal void FollowSuggestion(string suggestion)
    {
        if (!IsFolderNameEdited)
        {
            _folderName = suggestion;
        }
    }

    internal WorkspaceCreationRepository ToCreationRepository() => new(
        _folderName.Trim(),
        Repository.Path,
        _baseBranch.Trim(),
        Remote?.Name);
}
