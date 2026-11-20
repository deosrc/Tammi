namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// The workspace tabs that were open, as <see cref="OpenWorkspaceTabsStore"/> keeps them.
/// </summary>
/// <param name="WorkspaceIds">The ids of the workspaces open in tabs, in tab order.</param>
/// <param name="ActiveWorkspaceId">The workspace whose tab was shown, or <c>null</c> if another page was.</param>
public sealed record OpenWorkspaceTabs(IReadOnlyList<string> WorkspaceIds, string? ActiveWorkspaceId = null)
{
    /// <summary>
    /// The paths of the Local Repositories whose git graphs were open in tabs outside any workspace, in tab order.
    /// </summary>
    public IReadOnlyList<string> RepositoryPaths { get; init; } = [];

    /// <summary>
    /// The repository whose git graph tab was shown, or <c>null</c> if another page was. It is one of
    /// <see cref="RepositoryPaths"/>.
    /// </summary>
    public string? ActiveRepositoryPath { get; init; }

    /// <summary>
    /// For each repository in <see cref="RepositoryPaths"/>, the working directories (submodules and other worktrees)
    /// whose git graphs were open in its tab besides its own, in tab order. A repository with none has no entry.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> SecondaryGraphPaths { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>
    /// For each repository in <see cref="RepositoryPaths"/>, the working directory of the secondary git graph that its
    /// tab was showing. A repository that was showing its own graph (or a terminal or agent) has no entry.
    /// </summary>
    public IReadOnlyDictionary<string, string> ShownGraphPaths { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// No tabs.
    /// </summary>
    public static OpenWorkspaceTabs None { get; } = new([]);
}
