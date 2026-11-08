namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// A file a commit changed, from <see cref="IGitRepositoryReader.ReadCommitChanges"/>.
/// </summary>
/// <param name="Path">The path relative to the repository root, with <c>/</c> separators.</param>
/// <param name="Kind">How the file changed.</param>
/// <param name="OldPath">For a rename, the path the file was renamed from.</param>
public sealed record CommitFileChange(string Path, FileChangeKind Kind, string? OldPath = null);
