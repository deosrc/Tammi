namespace Deosrc.Tammi.Core.Git;

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
