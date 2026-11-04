namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// A workspace open in a tab.
/// </summary>
/// <param name="WorkspaceId">Identifies the workspace. Only one tab can be open for each workspace.</param>
/// <param name="Name">The workspace name, shown on the tab.</param>
public sealed record WorkspaceTab(string WorkspaceId, string Name);
