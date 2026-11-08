namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// Reads a repository's history a page at a time, from <see cref="IGitRepositoryReader.OpenHistory"/>. It keeps the
/// repository open, and the history walk prepared, until it's disposed, so later pages don't walk it again.
/// </summary>
/// <remarks>
/// It isn't thread-safe: read one page at a time. It does file I/O, so read off the UI thread.
/// </remarks>
public interface IGitHistoryCursor : IDisposable
{
    /// <summary>
    /// Whether every commit has been read.
    /// </summary>
    bool IsComplete { get; }

    /// <summary>
    /// Reads the next commits, newest first in topological order (children before parents).
    /// </summary>
    /// <param name="count">The most commits to read.</param>
    /// <returns>Fewer than <paramref name="count"/> commits only when the history ends.</returns>
    IReadOnlyList<CommitInfo> ReadNext(int count);
}
