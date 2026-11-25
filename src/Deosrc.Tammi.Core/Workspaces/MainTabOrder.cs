namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// The order of the tabs in the main tab strip, whatever they are: a new one is added at the end, and the user can move
/// them. It is kept here, not in the strip, so <see cref="WorkspaceTabsKeeper"/> can save it and open the tabs in the
/// same order.
/// </summary>
public sealed class MainTabOrder
{
    /// <summary>
    /// The key of the New Workspace tab.
    /// </summary>
    public const string NewWorkspaceKey = "new-workspace";

    /// <summary>
    /// The key of the Usage tab.
    /// </summary>
    public const string UsageKey = "usage";

    private const string WorkspacePrefix = "workspace:";
    private const string GraphPrefix = "git:";

    private readonly Lock _lock = new();
    private List<string> _keys = [];

    /// <summary>
    /// Raised when the order changes. It can be raised on any thread.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// The key of a workspace's tab.
    /// </summary>
    public static string WorkspaceKey(string workspaceId) => WorkspacePrefix + workspaceId;

    /// <summary>
    /// The key of the tab of a repository opened outside any workspace.
    /// </summary>
    public static string RepositoryKey(string repositoryPath) => GraphPrefix + repositoryPath;

    /// <summary>
    /// Whether a key is a workspace's tab.
    /// </summary>
    public static bool IsWorkspaceKey(string key) => key.StartsWith(WorkspacePrefix, StringComparison.Ordinal);

    /// <summary>
    /// Whether a key is a repository's tab.
    /// </summary>
    public static bool IsRepositoryKey(string key) => key.StartsWith(GraphPrefix, StringComparison.Ordinal);

    /// <summary>
    /// The keys of the tabs, in tab order.
    /// </summary>
    public IReadOnlyList<string> Keys
    {
        get
        {
            lock (_lock)
            {
                return [.. _keys];
            }
        }
    }

    /// <summary>
    /// Brings the order in line with the tabs that are open: closed tabs are dropped, and new ones are added at the
    /// end in the order given.
    /// </summary>
    /// <returns>The keys, in tab order.</returns>
    public IReadOnlyList<string> Sync(IReadOnlyList<string> open)
    {
        ArgumentNullException.ThrowIfNull(open);
        List<string> result;
        bool changed;
        lock (_lock)
        {
            result = [.. _keys.Where(open.Contains), .. open.Where(key => !_keys.Contains(key))];
            changed = !result.SequenceEqual(_keys);
            _keys = result;
        }

        if (changed)
        {
            Changed?.Invoke();
        }

        return [.. result];
    }

    /// <summary>
    /// Moves a tab to another place, as the user drags it.
    /// </summary>
    /// <param name="key">The tab to move. Nothing happens if it isn't in the order.</param>
    /// <param name="index">Its new place among the tabs, counted without it.</param>
    public void Move(string key, int index)
    {
        ArgumentNullException.ThrowIfNull(key);
        bool changed;
        lock (_lock)
        {
            if (!_keys.Contains(key))
            {
                return;
            }

            List<string> result = [.. _keys];
            result.Remove(key);
            result.Insert(Math.Clamp(index, 0, result.Count), key);
            changed = !result.SequenceEqual(_keys);
            _keys = result;
        }

        if (changed)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Sets the order, e.g. to the one saved last time. Tabs that aren't listed are added at the end by
    /// <see cref="Sync"/>.
    /// </summary>
    public void Set(IEnumerable<string> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        lock (_lock)
        {
            _keys = [.. keys.Distinct()];
        }

        Changed?.Invoke();
    }
}
