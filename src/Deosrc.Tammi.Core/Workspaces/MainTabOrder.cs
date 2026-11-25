namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// The order of the tabs in the main tab strip, whatever they are: a new one is added at the end, and the user can move
/// them. The Usage tab is always last. It is kept here, not in the strip, so <see cref="WorkspaceTabsKeeper"/> can save
/// it and open the tabs in the same order.
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
    /// end in the order given, before the Usage tab.
    /// </summary>
    /// <returns>The keys, in tab order.</returns>
    public IReadOnlyList<string> Sync(IReadOnlyList<string> open)
    {
        ArgumentNullException.ThrowIfNull(open);
        List<string> result;
        bool changed;
        lock (_lock)
        {
            result = UsageLast([.. _keys.Where(open.Contains), .. open.Where(key => !_keys.Contains(key))]);
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
    /// Moves a tab to another place, as the user drags it. The Usage tab stays last, so nothing moves after it.
    /// </summary>
    /// <param name="key">The tab to move. Nothing happens if it isn't in the order, or is the Usage tab.</param>
    /// <param name="index">Its new place among the tabs, counted without it.</param>
    public void Move(string key, int index)
    {
        ArgumentNullException.ThrowIfNull(key);
        bool changed;
        lock (_lock)
        {
            if (key == UsageKey || !_keys.Contains(key))
            {
                return;
            }

            List<string> result = [.. _keys];
            result.Remove(key);
            var last = result.Contains(UsageKey) ? result.Count - 1 : result.Count;
            result.Insert(Math.Clamp(index, 0, last), key);
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
            _keys = UsageLast([.. keys.Distinct()]);
        }

        Changed?.Invoke();
    }

    private static List<string> UsageLast(List<string> keys)
    {
        if (keys.Remove(UsageKey))
        {
            keys.Add(UsageKey);
        }

        return keys;
    }
}
