namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// A workspace that <see cref="WorkspaceCreator"/> created.
/// </summary>
/// <param name="Workspace">The workspace, as saved.</param>
/// <param name="Warnings">
/// Things that didn't stop it being created but the user should know, e.g. a repository that couldn't be fetched, so
/// its branch started from the last fetched commit.
/// </param>
public sealed record WorkspaceCreationResult(Workspace Workspace, IReadOnlyList<string> Warnings);
