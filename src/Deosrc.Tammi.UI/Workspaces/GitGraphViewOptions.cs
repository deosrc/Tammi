using Deosrc.Tammi.Core.Git;

namespace Deosrc.Tammi.UI.Workspaces;

/// <summary>
/// How the git graphs show their commits, chosen from their header's right-click menu: which columns are shown, and
/// which labels the rows have. Every graph shows the same, until the app closes. The Commit column is hidden until the
/// user shows it, and every label is shown.
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
