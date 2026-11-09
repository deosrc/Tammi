namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// The changes to one file, from <see cref="IGitRepositoryReader.ReadDiff"/>.
/// </summary>
/// <param name="Path">The file's path relative to the repository root, with <c>/</c> separators.</param>
/// <param name="OldPath">For a rename, the path the file was renamed from.</param>
/// <param name="Hunks">The changed regions. Empty for a binary file or a change with no text.</param>
/// <param name="IsBinary">Whether git treats the file as binary, so there's no text to show.</param>
/// <param name="IsTruncated">Whether the diff was cut short because it was too large to show.</param>
public sealed record FileDiff(
    string Path,
    string? OldPath,
    IReadOnlyList<DiffHunk> Hunks,
    bool IsBinary = false,
    bool IsTruncated = false)
{
    /// <summary>
    /// A diff of a file with no changes.
    /// </summary>
    public static FileDiff Empty(string path) => new(path, null, []);
}
