namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// The order the user has put each workspace's main area tabs in, by dragging them. A tab that hasn't been moved, such
/// as one opened since, goes after those that have, in the order the tabs started or opened.
/// </summary>
/// <remarks>
/// It's kept only while the app runs. <see cref="WorkspaceTabRestorer"/> and <see cref="WorkspaceTabsKeeper"/> save
/// the tabs in this order, and open them again in it, so it carries over to the next run.
/// </remarks>
public sealed class MainAreaTabArrangement
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, List<string>> _orders = [];

    /// <summary>
    /// Raised with the workspace's ID when the user moves one of its tabs. It can be raised on any thread.
    /// </summary>
    public event Action<string>? Changed;

    /// <summary>
    /// Puts a workspace's tabs in the order the user has arranged them in.
    /// </summary>
    public IReadOnlyList<T> Arrange<T>(string workspaceId, IEnumerable<T> tabs)
        where T : IMainAreaTab
    {
        ArgumentNullException.ThrowIfNull(workspaceId);
        ArgumentNullException.ThrowIfNull(tabs);
        List<string>? order;
        lock (_lock)
        {
            order = _orders.TryGetValue(workspaceId, out var found) ? [.. found] : null;
        }

        var byStart = tabs.OrderBy(tab => tab.StartOrder).ToList();
        if (order is null)
        {
            return byStart;
        }

        // Stable, so the tabs that haven't been moved keep the order they started in.
        return [.. byStart.OrderBy(tab => order.IndexOf(tab.Id) is var index and >= 0 ? index : int.MaxValue)];
    }

    /// <summary>
    /// Moves one of a workspace's tabs to another place among them.
    /// </summary>
    /// <param name="workspaceId">The workspace.</param>
    /// <param name="tabs">The tabs the user sees, in the order they're shown.</param>
    /// <param name="tabId">The ID of the tab to move. Nothing happens if it isn't one of <paramref name="tabs"/>.</param>
    /// <param name="index">Its new place among <paramref name="tabs"/>, counted without it.</param>
    public void Move(string workspaceId, IEnumerable<IMainAreaTab> tabs, string tabId, int index)
    {
        ArgumentNullException.ThrowIfNull(workspaceId);
        ArgumentNullException.ThrowIfNull(tabs);
        ArgumentNullException.ThrowIfNull(tabId);
        var ids = tabs.Select(tab => tab.Id).ToList();
        if (!ids.Remove(tabId))
        {
            return;
        }

        ids.Insert(Math.Clamp(index, 0, ids.Count), tabId);
        lock (_lock)
        {
            if (_orders.TryGetValue(workspaceId, out var current) && current.SequenceEqual(ids))
            {
                return;
            }

            // Only the tabs open now: those that have closed are forgotten.
            _orders[workspaceId] = ids;
        }

        Changed?.Invoke(workspaceId);
    }
}
