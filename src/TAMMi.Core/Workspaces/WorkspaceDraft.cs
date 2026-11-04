using TAMMi.Core.Integrations;
using TAMMi.Core.Settings;

namespace TAMMi.Core.Workspaces;

/// <summary>
/// The choices made so far in the Create Workspace flow: the tasks, the workspace's name and its parent folder.
/// </summary>
/// <remarks>
/// <para>
/// The name follows the tasks (see <see cref="WorkspaceNames.Suggest"/>) until the user edits it. Clearing it makes it
/// follow them again the next time they change, but doesn't fill it in straight away, so the user can type over it.
/// </para>
/// <para>
/// The checks look at the disk each time they're read, so they see folders created since the last check.
/// </para>
/// </remarks>
public sealed class WorkspaceDraft
{
    private readonly List<TaskInfo> _tasks = [];
    private readonly WorkspaceNameFormat _nameFormat;
    private string _name = string.Empty;

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
