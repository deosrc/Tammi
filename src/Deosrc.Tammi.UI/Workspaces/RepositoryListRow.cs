using Deosrc.Tammi.Core.Catalogue;
using Deosrc.Tammi.Core.Git;

namespace Deosrc.Tammi.UI.Workspaces;

/// <summary>
/// A row of the Repositories page: a catalogued repository and its git state, once that's been read.
/// </summary>
/// <param name="Repository">The catalogued repository.</param>
/// <param name="IsLoaded">Whether the state has been read yet. Until then the row shows placeholders.</param>
/// <param name="Status">The git status, or <see langword="null"/> if it hasn't been read or couldn't be.</param>
/// <param name="WorkspaceCount">How many workspaces have a worktree of the repository.</param>
/// <param name="Problem">Why the status couldn't be read, or <see langword="null"/>.</param>
public sealed record RepositoryListRow(
    CatalogueRepository Repository,
    bool IsLoaded,
    RepositoryStatus? Status,
    int WorkspaceCount,
    string? Problem);
