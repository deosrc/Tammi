namespace TAMMi.Core.Git;

/// <summary>
/// The git state of a repository (or Workspace Worktree), as shown on Home and in the Workspace View.
/// </summary>
/// <remarks>
/// A repository is <see cref="IsChanged">changed</see> when it holds work that could be lost: uncommitted changes
/// (staged, unstaged or untracked) or unpushed commits, in the repository itself or in any of its submodules.
/// Being <see cref="IsBehind">behind</see> the upstream is reported separately and doesn't count as changed.
/// </remarks>
public sealed record RepositoryStatus
{
    /// <summary>
    /// The repository's working directory.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// The checked-out branch's short name, or <see langword="null"/> when HEAD is detached (as is usual for
    /// submodules).
    /// </summary>
    public string? Branch { get; init; }

    /// <summary>
    /// The commit HEAD points at, or <see langword="null"/> in a repository with no commits yet.
    /// </summary>
    public string? HeadSha { get; init; }

    /// <summary>
    /// The upstream branch's name (e.g. <c>origin/main</c>), or <see langword="null"/> when the branch doesn't track
    /// one.
    /// </summary>
    public string? Upstream { get; init; }

    /// <summary>
    /// Commits on HEAD that aren't on the upstream, or <see langword="null"/> without an upstream.
    /// </summary>
    public int? AheadBy { get; init; }

    /// <summary>
    /// Commits on the upstream that aren't on HEAD, or <see langword="null"/> without an upstream.
    /// </summary>
    public int? BehindBy { get; init; }

    /// <summary>
    /// Commits on HEAD that aren't on any remote-tracking branch, i.e. commits that exist only locally. This
    /// doesn't depend on the upstream, so it also works for new branches and detached HEADs. It's 0 for a repository
    /// with no remotes.
    /// </summary>
    public int UnpushedCommitCount { get; init; }

    /// <summary>
    /// Changed files, excluding ignored files and submodules.
    /// </summary>
    public IReadOnlyList<FileChange> Changes { get; init; } = [];

    /// <summary>
    /// The repository's submodules, each with its own status when it's checked out.
    /// </summary>
    public IReadOnlyList<SubmoduleStatus> Submodules { get; init; } = [];

    /// <summary>
    /// Whether this repository has staged, unstaged or untracked changes, including changed submodule commits.
    /// Changes inside submodules are counted by <see cref="IsChanged"/>.
    /// </summary>
    public bool HasUncommittedChanges =>
        Changes.Count > 0 || Submodules.Any(submodule => submodule.CommitChange != SubmoduleCommitChange.None);

    /// <summary>
    /// Whether this repository has commits that exist only locally.
    /// </summary>
    public bool HasUnpushedCommits => UnpushedCommitCount > 0;

    /// <summary>
    /// Whether the repository or any of its submodules holds work that could be lost.
    /// </summary>
    public bool IsChanged =>
        HasUncommittedChanges
            || HasUnpushedCommits
            || Submodules.Any(submodule => submodule.Status?.IsChanged == true);

    /// <summary>
    /// Whether the upstream has commits that HEAD doesn't. Informational: it doesn't count as changed.
    /// </summary>
    public bool IsBehind => BehindBy > 0;
}

/// <summary>
/// A changed file in a repository.
/// </summary>
/// <param name="Path">The path relative to the repository root, with <c>/</c> separators.</param>
/// <param name="Staged">The change between HEAD and the index.</param>
/// <param name="Unstaged">The change between the index and the working directory.</param>
/// <param name="OldPath">For a staged rename, the path the file was renamed from.</param>
public sealed record FileChange(string Path, FileChangeKind Staged, FileChangeKind Unstaged, string? OldPath = null)
{
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
    /// Whether the file has unresolved merge conflicts.
    /// </summary>
    public bool IsConflicted => Staged == FileChangeKind.Conflicted;
}

/// <summary>
/// How a file changed between two states (HEAD, index or working directory).
/// </summary>
public enum FileChangeKind
{
    /// <summary>No change.</summary>
    None,

    /// <summary>A new file. In the index: staged for the first time.</summary>
    Added,

    /// <summary>The file's content changed.</summary>
    Modified,

    /// <summary>The file was deleted.</summary>
    Deleted,

    /// <summary>The file was renamed.</summary>
    Renamed,

    /// <summary>The file's type changed, e.g. from a regular file to a symlink.</summary>
    TypeChanged,

    /// <summary>A file in the working directory that git doesn't track. Only used for unstaged changes.</summary>
    Untracked,

    /// <summary>The file has unresolved merge conflicts. Used for both staged and unstaged changes.</summary>
    Conflicted,
}

/// <summary>
/// A submodule of a repository.
/// </summary>
/// <param name="Name">The submodule's name in <c>.gitmodules</c>.</param>
/// <param name="Path">The path relative to the parent repository root, with <c>/</c> separators.</param>
/// <param name="CommitChange">
/// How the commit the parent repository records for the submodule has changed. A changed commit is an uncommitted
/// change in the parent repository.
/// </param>
/// <param name="Status">
/// The submodule's own status, or <see langword="null"/> when it isn't checked out.
/// </param>
public sealed record SubmoduleStatus(
    string Name,
    string Path,
    SubmoduleCommitChange CommitChange,
    RepositoryStatus? Status)
{
    /// <summary>
    /// Whether the submodule is checked out (<c>git submodule update --init</c> has run).
    /// </summary>
    public bool IsInitialized => Status is not null;
}

/// <summary>
/// How the submodule commit recorded in the parent repository has changed.
/// </summary>
[Flags]
public enum SubmoduleCommitChange
{
    /// <summary>The parent repository records the commit the submodule has checked out.</summary>
    None = 0,

    /// <summary>
    /// The index records a different commit from HEAD, or the submodule was added or removed in the index.
    /// </summary>
    Staged = 1,

    /// <summary>
    /// The submodule has a different commit checked out from the one in the index, or its folder was removed.
    /// </summary>
    Unstaged = 2,
}
