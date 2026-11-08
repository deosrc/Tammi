using LibGit2Sharp;

namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// Reads commits from a LibGit2Sharp history walk, a page at a time.
/// </summary>
internal sealed class GitHistoryCursor : IGitHistoryCursor
{
    private readonly Repository _repository;
    private readonly IEnumerator<Commit>? _commits;
    private bool _disposed;

    /// <summary>
    /// Takes ownership of <paramref name="repository"/>, and disposes it with the cursor.
    /// </summary>
    /// <param name="repository">The open repository.</param>
    /// <param name="startingPoints">The commits to walk from. Empty means there's nothing to read.</param>
    public GitHistoryCursor(Repository repository, IReadOnlyList<Commit> startingPoints)
    {
        _repository = repository;
        if (startingPoints.Count == 0)
        {
            IsComplete = true;
            return;
        }

        _commits = repository.Commits.QueryBy(new CommitFilter
        {
            IncludeReachableFrom = startingPoints,
            SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Time,
        }).GetEnumerator();
    }

    /// <inheritdoc />
    public bool IsComplete { get; private set; }

    /// <inheritdoc />
    public IReadOnlyList<CommitInfo> ReadNext(int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        var page = new List<CommitInfo>(Math.Min(count, 1000));
        while (!IsComplete && page.Count < count)
        {
            if (_commits!.MoveNext())
            {
                page.Add(GitRepositoryReader.ToCommitInfo(_commits.Current));
            }
            else
            {
                IsComplete = true;
            }
        }

        return page;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _commits?.Dispose();
        _repository.Dispose();
    }
}
