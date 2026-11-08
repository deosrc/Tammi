using Deosrc.Tammi.Core.Git;

namespace Deosrc.Tammi.UI.Workspaces;

/// <summary>
/// How the git graphs show their commits, chosen from their header's right-click menu (which columns are shown, and
/// which labels the rows have) and in their details (how changed files are listed). Every graph shows the same, until
/// the app closes. The Commit column is hidden until the user shows it, every label is shown, files are listed, and the
/// Authors section is open.
/// </summary>
public sealed class GitGraphViewOptions
{
    private readonly HashSet<GitGraphColumn> _hidden = [GitGraphColumn.Commit];

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
    public GitGraphFileView FileView { get; private set; } = GitGraphFileView.List;

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
    /// Lists the changed files, or shows them in their folders.
    /// </summary>
    public void SetFileView(GitGraphFileView view)
    {
        if (view != FileView)
        {
            FileView = view;
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
