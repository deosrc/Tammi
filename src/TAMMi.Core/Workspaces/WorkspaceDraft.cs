using TAMMi.Core.Catalogue;
using TAMMi.Core.Git;
using TAMMi.Core.Integrations;
using TAMMi.Core.Settings;

namespace TAMMi.Core.Workspaces;

/// <summary>
/// The choices made so far in the Create Workspace flow: the tasks, the workspace's name and its parent folder, then the
/// repositories and the branch to create in them.
/// </summary>
/// <remarks>
/// <para>
/// The name follows the tasks (see <see cref="WorkspaceNames.Suggest"/>) until the user edits it. Clearing it makes it
/// follow them again the next time they change, but doesn't fill it in straight away, so the user can type over it.
/// </para>
/// <para>
/// The branch name follows the tasks and the name (see <see cref="WorkspaceBranchNames.Suggest"/>) in the same way, and
/// so do the repositories' folder names (see <see cref="WorktreeFolderNames.Suggest"/>).
/// </para>
/// <para>
/// The checks look at the disk each time they're read, so they see folders created since the last check.
/// </para>
/// </remarks>
public sealed class WorkspaceDraft
{
    private readonly List<TaskInfo> _tasks = [];
    private readonly List<WorkspaceDraftRepository> _repositories = [];
    private readonly HashSet<TaskReference> _tasksWithRepositoriesChosen = [];
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private readonly WorkspaceNameFormat _nameFormat;
    private string _name = string.Empty;
    private string _branchName = string.Empty;

    /// <param name="parentFolder">
    /// The parent folder to start with, usually the Default Workspace Parent Folder setting.
    /// </param>
    /// <param name="nameFormat">How the suggested name is written. The default when <see langword="null"/>.</param>
    public WorkspaceDraft(string parentFolder, WorkspaceNameFormat? nameFormat = null)
    {
        ArgumentNullException.ThrowIfNull(parentFolder);
        ParentFolder = parentFolder;
        _nameFormat = nameFormat ?? new WorkspaceNameFormat();
    }

    /// <summary>
    /// The chosen tasks, in the order they were chosen. A workspace can have none, and can mix tasks from any sources.
    /// </summary>
    public IReadOnlyList<TaskInfo> Tasks => _tasks;

    /// <summary>
    /// The workspace's name, as entered. Setting it to anything but blank stops it following the tasks, and clearing it makes it
    /// follow them again.
    /// </summary>
    public string Name
    {
        get => _name;
        set
        {
            _name = value ?? string.Empty;
            IsNameEdited = !string.IsNullOrWhiteSpace(_name);
            FollowBranchNameSuggestion();
        }
    }

    /// <summary>
    /// Whether the user entered the <see cref="Name"/>, rather than it being suggested from the tasks.
    /// </summary>
    public bool IsNameEdited { get; private set; }

    /// <summary>
    /// The name suggested for the chosen tasks, or <see langword="null"/> if there's none.
    /// </summary>
    public string? SuggestedName => WorkspaceNames.Suggest(_tasks, _nameFormat);

    /// <summary>
    /// The folder the Workspace Folder is created in, as entered.
    /// </summary>
    public string ParentFolder { get; set; }

    /// <summary>
    /// Why the <see cref="Name"/> can't be used, or <see langword="null"/> if it can.
    /// </summary>
    public string? NameProblem => WorkspaceNames.Validate(_name);

    /// <summary>
    /// Why the <see cref="ParentFolder"/> can't be used, or <see langword="null"/> if it can. It needn't exist yet.
    /// </summary>
    public string? ParentFolderProblem => ValidateParentFolder(ParentFolder, out _);

    /// <summary>
    /// The Workspace Folder's full path, <c>&lt;parent folder&gt;/&lt;name&gt;</c>, or <see langword="null"/> while
    /// the name or parent folder can't be used.
    /// </summary>
    public string? Folder =>
        NameProblem is null && ValidateParentFolder(ParentFolder, out string? parent) is null
            ? Path.Combine(parent!, _name.Trim())
            : null;

    /// <summary>
    /// Why the Workspace Folder can't be created, e.g. it already exists, or <see langword="null"/> if it can. While
    /// the name or parent folder can't be used, this is <see langword="null"/> too: see their own problems.
    /// </summary>
    public string? FolderProblem => Folder is { } folder && (Directory.Exists(folder) || File.Exists(folder))
        ? $"'{folder}' already exists. Choose another name or parent folder."
        : null;

    /// <summary>
    /// Whether the parent folder doesn't exist yet, so creating the workspace creates it too.
    /// </summary>
    public bool ParentFolderIsNew =>
        ValidateParentFolder(ParentFolder, out string? parent) is null && !Directory.Exists(parent);

    /// <summary>
    /// Whether the name and location can be used, so the flow can go on to the repositories.
    /// </summary>
    public bool IsLocationValid => Folder is not null && FolderProblem is null;

    /// <summary>
    /// The chosen repositories, in the order they were chosen.
    /// </summary>
    public IReadOnlyList<WorkspaceDraftRepository> Repositories => _repositories;

    /// <summary>
    /// The branch to create in every repository, as entered or suggested. Setting it to anything but blank stops it
    /// following the suggestion, and clearing it makes it follow again the next time the tasks or name change.
    /// </summary>
    public string BranchName
    {
        get => _branchName;
        set
        {
            _branchName = value ?? string.Empty;
            IsBranchNameEdited = !string.IsNullOrWhiteSpace(_branchName);
        }
    }

    /// <summary>
    /// Whether the user entered the <see cref="BranchName"/>.
    /// </summary>
    public bool IsBranchNameEdited { get; private set; }

    /// <summary>
    /// The branch name suggested for the chosen tasks and name, or <see langword="null"/> if there's none.
    /// </summary>
    public string? SuggestedBranchName => WorkspaceBranchNames.Suggest(_tasks, _name);

    /// <summary>
    /// Whether the repository is chosen.
    /// </summary>
    public bool ContainsRepository(CatalogueRepository repository) => FindRepository(repository) is not null;

    /// <summary>
    /// Returns the chosen repository for a catalogue entry, or <see langword="null"/> if it isn't chosen.
    /// </summary>
    public WorkspaceDraftRepository? FindRepository(CatalogueRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return _repositories.Find(chosen => PathComparer.Equals(chosen.Repository.Path, repository.Path));
    }

    /// <summary>
    /// Chooses a repository. Its folder name is suggested; call <see cref="WorkspaceDraftRepository.LoadAsync"/> to
    /// find its base branch.
    /// </summary>
    /// <returns>The chosen repository, or <see langword="null"/> if it was chosen already.</returns>
    public WorkspaceDraftRepository? AddRepository(CatalogueRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if (ContainsRepository(repository))
        {
            return null;
        }

        var chosen = new WorkspaceDraftRepository(this, repository);
        _repositories.Add(chosen);
        FollowFolderNameSuggestions();
        return chosen;
    }

    /// <summary>
    /// Chooses the repositories the tasks belong to (see <see cref="TaskInfo.RepositoryUrl"/>), and removes those it
    /// chose for tasks that are no longer chosen. Call it when the repository step is shown.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A task's repository is the Local Repository with a remote matching its URL. When several clones match, the one
    /// whose fetched remote matches is preferred. Nothing is added when one of them is already chosen.
    /// </para>
    /// <para>
    /// Each task's repository is chosen only once, so a repository the user removes stays removed.
    /// </para>
    /// </remarks>
    /// <param name="catalogue">The Repository Catalogue's repositories.</param>
    /// <returns>The repositories it chose, so their base branches can be loaded.</returns>
    public IReadOnlyList<WorkspaceDraftRepository> ChooseTaskRepositories(IReadOnlyList<CatalogueRepository> catalogue)
    {
        ArgumentNullException.ThrowIfNull(catalogue);

        // Repositories chosen only for tasks that have since been removed.
        _tasksWithRepositoriesChosen.RemoveWhere(task => !ContainsTask(task));
        bool removed = _repositories.RemoveAll(repository =>
            repository.ChosenForTasks.Count > 0 && !repository.ChosenForTasks.Any(ContainsTask)) > 0;

        var added = new List<WorkspaceDraftRepository>();
        foreach (var task in _tasks)
        {
            if (task.RepositoryUrl is not { } url || !_tasksWithRepositoriesChosen.Add(task.Reference))
            {
                continue;
            }

            var matches = catalogue
                .Where(repository => repository.Remotes.Any(remote => GitRemoteUrls.IsSameRepository(remote.Url, url.ToString())))
                .ToList();
            if (_repositories.Find(chosen => matches.Exists(match => PathComparer.Equals(match.Path, chosen.Repository.Path)))
                is { } alreadyChosen)
            {
                if (alreadyChosen.ChosenForTasks.Count > 0)
                {
                    alreadyChosen.AddChosenForTask(task.Reference);
                }

                continue;
            }

            if (matches.Count == 0)
            {
                continue;
            }

            var match = matches.Find(repository =>
                    WorkspaceDraftRepository.ChooseRemote(repository) is { } remote
                    && GitRemoteUrls.IsSameRepository(remote.Url, url.ToString()))
                ?? matches[0];
            var chosen = new WorkspaceDraftRepository(this, match);
            chosen.AddChosenForTask(task.Reference);
            _repositories.Add(chosen);
            added.Add(chosen);
        }

        if (removed || added.Count > 0)
        {
            FollowFolderNameSuggestions();
        }

        return added;
    }

    /// <summary>
    /// Removes a chosen repository.
    /// </summary>
    /// <returns>Whether it was chosen.</returns>
    public bool RemoveRepository(CatalogueRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if (_repositories.RemoveAll(chosen => PathComparer.Equals(chosen.Repository.Path, repository.Path)) == 0)
        {
            return false;
        }

        FollowFolderNameSuggestions();
        return true;
    }

    /// <summary>
    /// Why the <see cref="BranchName"/> can't be used, or <see langword="null"/> if it might. Git has the final say on
    /// a branch name (see <see cref="Git.IGitWorktreeService.IsValidBranchNameAsync"/>).
    /// </summary>
    public string? BranchNameProblem => string.IsNullOrWhiteSpace(_branchName) ? "Enter a branch name." : null;

    /// <summary>
    /// Whether the repositories and branch can be used as far as the draft can tell, so the workspace can be created.
    /// <see cref="WorkspaceCreator.CheckAsync"/> checks the rest, such as whether the branch exists.
    /// </summary>
    public bool IsRepositoriesValid =>
        BranchNameProblem is null
        && _repositories.TrueForAll(repository =>
            repository.FolderNameProblem is null && !repository.IsLoading && repository.BaseBranch.Trim().Length > 0);

    /// <summary>
    /// The request to create the workspace as drafted.
    /// </summary>
    /// <exception cref="InvalidOperationException">The name or location can't be used.</exception>
    public WorkspaceCreationRequest ToCreationRequest() => new()
    {
        Name = _name.Trim(),
        Folder = Folder ?? throw new InvalidOperationException("The workspace's name or location isn't valid."),
        Tasks = [.. _tasks.Select(task => task.Reference)],
        BranchName = _branchName.Trim(),
        Repositories = [.. _repositories.Select(repository => repository.ToCreationRepository())],
    };

    /// <summary>
    /// Whether the task is chosen.
    /// </summary>
    public bool ContainsTask(TaskReference task) => _tasks.Exists(chosen => chosen.Reference == task);

    /// <summary>
    /// Chooses a task.
    /// </summary>
    /// <returns>Whether it was added, i.e. it wasn't chosen already.</returns>
    public bool AddTask(TaskInfo task)
    {
        ArgumentNullException.ThrowIfNull(task);
        if (ContainsTask(task.Reference))
        {
            return false;
        }

        _tasks.Add(task);
        FollowTasks();
        return true;
    }

    /// <summary>
    /// Removes a chosen task.
    /// </summary>
    /// <returns>Whether it was chosen.</returns>
    public bool RemoveTask(TaskReference task)
    {
        ArgumentNullException.ThrowIfNull(task);
        if (_tasks.RemoveAll(chosen => chosen.Reference == task) == 0)
        {
            return false;
        }

        FollowTasks();
        return true;
    }

    private void FollowTasks()
    {
        if (!IsNameEdited)
        {
            _name = SuggestedName ?? string.Empty;
        }

        FollowBranchNameSuggestion();
    }

    private void FollowBranchNameSuggestion()
    {
        if (!IsBranchNameEdited)
        {
            _branchName = SuggestedBranchName ?? string.Empty;
        }
    }

    /// <summary>
    /// Updates the folder names that follow their suggestions, after the repositories or a folder name changed.
    /// </summary>
    /// <param name="except">A repository whose folder name the user just cleared, so it isn't filled in again yet.</param>
    internal void FollowFolderNameSuggestions(WorkspaceDraftRepository? except = null)
    {
        var suggestions = WorktreeFolderNames.Suggest(
        [
            .. _repositories.Select(repository => (
                repository.Repository.Name,
                repository.Owner,
                repository.IsFolderNameEdited ? repository.FolderName.Trim() : null)),
        ]);
        for (int i = 0; i < _repositories.Count; i++)
        {
            if (_repositories[i] != except)
            {
                _repositories[i].FollowSuggestion(suggestions[i]);
            }
        }
    }

    /// <summary>
    /// Returns why a parent folder can't be used, or <see langword="null"/> with its full, tidied path.
    /// </summary>
    private static string? ValidateParentFolder(string? folder, out string? fullPath)
    {
        fullPath = null;
        folder = folder?.Trim();
        if (string.IsNullOrEmpty(folder))
        {
            return "Choose a parent folder.";
        }

        if (!Path.IsPathFullyQualified(folder))
        {
            return "The parent folder must be a full path.";
        }

        try
        {
            fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return $"The parent folder isn't a valid path. {ex.Message}";
        }

        if (File.Exists(fullPath))
        {
            fullPath = null;
            return $"'{folder}' is a file, not a folder.";
        }

        return null;
    }
}
