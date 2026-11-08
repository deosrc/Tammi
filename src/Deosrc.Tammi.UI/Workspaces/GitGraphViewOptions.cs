using Deosrc.Tammi.Core.Git;
using Deosrc.Tammi.Core.Settings;

namespace Deosrc.Tammi.UI.Workspaces;

/// <summary>
/// How the git graphs show their commits, chosen from their header's right-click menu (which columns are shown, and
/// which labels the rows have) and in their details (how changed files are shown, and whether Authors is collapsed).
/// Every graph shows the same. How changed files are shown is kept in the settings; the rest lasts until the app closes.
/// The Commit column is hidden until the user shows it, every label is shown, and the Authors section is open.
/// </summary>
public sealed class GitGraphViewOptions : IDisposable
{
    private readonly HashSet<GitGraphColumn> _hidden = [GitGraphColumn.Commit];
    private readonly ISettingsStore _settings;
    private GitGraphFileView _fileView;

    /// <summary>
    /// Creates the options, showing changed files as the settings say.
    /// </summary>
    public GitGraphViewOptions(ISettingsStore settings)
    {
        _settings = settings;
        _fileView = settings.Current.Git.GraphFileView;
        _settings.Changed += OnSettingsChanged;
    }

    /// <summary>
    /// Raised when an option changes, on the thread that changed it.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// The columns shown, in order.
    /// </summary>
    public IReadOnlyList<GitGraphColumn> VisibleColumns =>
        [.. Enum.GetValues<GitGraphColumn>().Where(column => !_hidden.Contains(column))];

    /// <summary>
    /// Whether the rows show tags.
    /// </summary>
    public bool ShowTags { get; private set; } = true;

    /// <summary>
    /// Whether the rows show remote-tracking branches.
    /// </summary>
    public bool ShowRemoteBranches { get; private set; } = true;

    /// <summary>
    /// How the details show the selected commit's changed files.
    /// </summary>
    public GitGraphFileView FileView => _fileView;

    /// <summary>
    /// Whether the details' Authors section is collapsed, showing only the author's name.
    /// </summary>
    public bool AuthorsCollapsed { get; private set; }

    /// <summary>
    /// Whether a column can be hidden. Graph can't, since it's what the view is for, and nor can Description, since it
    /// takes the space the others leave.
    /// </summary>
    public static bool CanHide(GitGraphColumn column) =>
        column is not (GitGraphColumn.Graph or GitGraphColumn.Description);

    /// <summary>
    /// Whether a column is shown.
    /// </summary>
    public bool IsVisible(GitGraphColumn column) => !_hidden.Contains(column);

    /// <summary>
    /// Whether the rows show a branch or tag's label. Local branches are always shown.
    /// </summary>
    public bool Shows(GitReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return reference.Kind switch
        {
            GitReferenceKind.Tag => ShowTags,
            GitReferenceKind.RemoteBranch => ShowRemoteBranches,
            _ => true,
        };
    }

    /// <summary>
    /// Shows a hidden column, or hides a shown one. Graph and Description are always shown.
    /// </summary>
    public void ToggleColumn(GitGraphColumn column)
    {
        if (!CanHide(column))
        {
            return;
        }

        if (!_hidden.Remove(column))
        {
            _hidden.Add(column);
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Lists the changed files, or shows them in their folders, and keeps the choice in the settings. The graphs show
    /// it straight away. If the settings can't be saved, they show it until the app closes.
    /// </summary>
    public async Task SetFileViewAsync(GitGraphFileView view)
    {
        if (view == _fileView)
        {
            return;
        }

        _fileView = view;
        Changed?.Invoke();
        try
        {
            await _settings.UpdateAsync(settings => settings with
            {
                Git = settings.Git with { GraphFileView = view },
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not kept for next time, which only means the details show files the old way after a restart.
        }
    }

    /// <summary>
    /// Stops following the settings.
    /// </summary>
    public void Dispose() => _settings.Changed -= OnSettingsChanged;

    // The settings changed, e.g. by another graph's choice being saved. Only a different choice changes the graphs.
    private void OnSettingsChanged()
    {
        var view = _settings.Current.Git.GraphFileView;
        if (view != _fileView)
        {
            _fileView = view;
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Collapses the details' Authors section if it's open, or opens it.
    /// </summary>
    public void ToggleAuthors()
    {
        AuthorsCollapsed = !AuthorsCollapsed;
        Changed?.Invoke();
    }

    /// <summary>
    /// Shows the rows' tags if they're hidden, or hides them.
    /// </summary>
    public void ToggleTags()
    {
        ShowTags = !ShowTags;
        Changed?.Invoke();
    }

    /// <summary>
    /// Shows the rows' remote-tracking branches if they're hidden, or hides them.
    /// </summary>
    public void ToggleRemoteBranches()
    {
        ShowRemoteBranches = !ShowRemoteBranches;
        Changed?.Invoke();
    }
}
