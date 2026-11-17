namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// A worktree of a repository, as <c>git worktree list</c> reports it.
/// </summary>
/// <param name="Path">The worktree's full path.</param>
/// <param name="Branch">The branch's short name, or <see langword="null"/> when HEAD is detached or it's bare.</param>
/// <param name="IsMain">Whether it is the repository's main working tree, which git lists first.</param>
public sealed record GitWorktreeEntry(string Path, string? Branch, bool IsMain);
