namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// A changed file in a repository.
/// </summary>
/// <param name="Path">The path relative to the repository root, with <c>/</c> separators.</param>
/// <param name="Staged">The change between HEAD and the index.</param>
/// <param name="Unstaged">The change between the index and the working directory.</param>
/// <param name="OldPath">For a rename, staged or not, the path the file was renamed from.</param>
/// <param name="IsSubmodule">
/// Whether the path is a submodule, and the change is to the commit the repository records for it.
/// </param>
public sealed record FileChange(
    string Path,
    FileChangeKind Staged,
    FileChangeKind Unstaged,
    string? OldPath = null,
    bool IsSubmodule = false)
{
    /// <summary>
    /// The change to the commit a repository records for a submodule, or <see langword="null"/> if the submodule's
    /// checked-out commit is the recorded one. Files in the submodule that aren't committed aren't part of it.
    /// </summary>
    public static FileChange? ForSubmodule(SubmoduleStatus submodule)
    {
        ArgumentNullException.ThrowIfNull(submodule);
        if (submodule.CommitChange == SubmoduleCommitChange.None)
        {
            return null;
        }

        return new FileChange(
            submodule.Path,
            submodule.CommitChange.HasFlag(SubmoduleCommitChange.Staged) ? FileChangeKind.Modified : FileChangeKind.None,
            submodule.CommitChange.HasFlag(SubmoduleCommitChange.Unstaged) ? FileChangeKind.Modified : FileChangeKind.None,
            IsSubmodule: true);
    }

    /// <summary>
    /// Whether the file has changes in the index.
    /// </summary>
    public bool IsStaged => Staged is not (FileChangeKind.None or FileChangeKind.Conflicted);

    /// <summary>
    /// Whether the file has changes in the working directory that aren't staged. Untracked files count.
    /// </summary>
    public bool IsUnstaged => Unstaged is not (FileChangeKind.None or FileChangeKind.Conflicted);

    /// <summary>
    /// Whether git doesn't track the file yet.
    /// </summary>
    public bool IsUntracked => Unstaged == FileChangeKind.Untracked;

    /// <summary>
    /// Whether the file was renamed in the working directory, and the rename isn't staged: git sees the old file as
    /// deleted, and the new one as untracked.
    /// </summary>
    public bool IsUnstagedRename => Unstaged == FileChangeKind.Renamed && OldPath is not null;

    /// <summary>
    /// Whether the file has unresolved merge conflicts.
    /// </summary>
    public bool IsConflicted => Staged == FileChangeKind.Conflicted;
}
