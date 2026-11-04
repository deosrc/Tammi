namespace TAMMi.Core.Git;

/// <summary>
/// Which commits <see cref="IGitRepositoryReader.ReadHistory"/> returns.
/// </summary>
public sealed record HistoryOptions
{
    /// <summary>
    /// The most commits to return, or <see langword="null"/> for all of them.
    /// </summary>
    public int? MaxCount { get; init; } = 500;

    /// <summary>
    /// Include commits reachable from every local and remote-tracking branch and tag, not just HEAD. Use for the
    /// git graph.
    /// </summary>
    public bool AllRefs { get; init; }
}
