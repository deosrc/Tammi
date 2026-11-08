namespace Deosrc.Tammi.UI.Workspaces;

/// <summary>
/// Which columns the git graphs show, chosen from their header's right-click menu. Every graph shows the same columns,
/// until the app closes. The Commit column is hidden until the user shows it.
/// </summary>
public sealed class GitGraphColumns
{
    private readonly HashSet<GitGraphColumn> _hidden = [GitGraphColumn.Commit];

    /// <summary>
    /// Raised when a column is shown or hidden, on the thread that changed it.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// The columns shown, in order.
    /// </summary>
    public IReadOnlyList<GitGraphColumn> Visible =>
        [.. Enum.GetValues<GitGraphColumn>().Where(column => !_hidden.Contains(column))];

    /// <summary>
    /// Whether a column can be hidden. Description can't, since it takes the space the others leave.
    /// </summary>
    public static bool CanHide(GitGraphColumn column) => column != GitGraphColumn.Description;

    /// <summary>
    /// Whether a column is shown.
    /// </summary>
    public bool IsVisible(GitGraphColumn column) => !_hidden.Contains(column);

    /// <summary>
    /// Shows a hidden column, or hides a shown one. Description is always shown.
    /// </summary>
    public void Toggle(GitGraphColumn column)
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
}
