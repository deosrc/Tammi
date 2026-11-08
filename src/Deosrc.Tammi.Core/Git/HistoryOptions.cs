namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// Which commits <see cref="IGitRepositoryReader.ReadHistory"/> and <see cref="IGitRepositoryReader.OpenHistory"/>
/// return.
/// </summary>
public sealed record HistoryOptions
{
    /// <summary>
    /// The most commits <see cref="IGitRepositoryReader.ReadHistory"/> returns, or <see langword="null"/> for all of
    /// them. <see cref="IGitRepositoryReader.OpenHistory"/> ignores it: its caller asks for each page.
    /// </summary>
    public int? MaxCount { get; init; } = 500;

    /// <summary>
    /// Include commits reachable from every local and remote-tracking branch and tag, not just HEAD. When set,
    /// <see cref="StartingPoints"/> and <see cref="IncludeUpstreams"/> are ignored.
    /// </summary>
    public bool AllRefs { get; init; }

    /// <summary>
    /// The commits to walk from instead of HEAD, as anything git can resolve to a commit: <c>HEAD</c>, a branch name
    /// (e.g. <c>feature/x</c> or <c>origin/main</c>), a tag or a commit id. Names that don't resolve are skipped, so a
    /// branch that's been deleted doesn't stop the rest being read. Empty means HEAD.
    /// </summary>
    public IReadOnlyList<string> StartingPoints { get; init; } = [];

    /// <summary>
    /// Also walk from the upstream of each local branch in <see cref="StartingPoints"/> (or HEAD's branch, when it's
    /// HEAD), when it has one.
    /// </summary>
    public bool IncludeUpstreams { get; init; }
}
