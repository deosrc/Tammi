namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// Reads a file's diff as <see cref="GitDiffOptions"/> say, using the in-process reader when it can and the git CLI
/// when the diff ignores whitespace.
/// </summary>
public interface IGitDiffs
{
    /// <summary>
    /// Reads the changes to one file as hunks of lines.
    /// </summary>
    /// <param name="path">The repository's working directory.</param>
    /// <param name="source">Which two states to compare.</param>
    /// <param name="file">The file's path relative to the repository root, with <c>/</c> separators.</param>
    /// <param name="options">What the diff is computed with.</param>
    /// <param name="oldPath">For a rename, the path the file was renamed from.</param>
    /// <param name="sha">For <see cref="GitDiffSource.Commit"/>, the commit's full id.</param>
    /// <param name="cancellationToken">Cancelling kills git.</param>
    Task<FileDiff> ReadAsync(
        string path,
        GitDiffSource source,
        string file,
        GitDiffOptions options,
        string? oldPath = null,
        string? sha = null,
        CancellationToken cancellationToken = default);
}
