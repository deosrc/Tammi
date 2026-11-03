namespace TAMMi.UI.Workspaces;

/// <summary>
/// Sample workspaces shown on Home until workspaces can be created and loaded.
/// </summary>
internal static class PlaceholderWorkspaces
{
    public static IReadOnlyList<PlaceholderWorkspace> All { get; } =
    [
        new("fix-login-timeout", "#123 Fix login timeout", "2/3 repos changed · 1 agent needs input"),
        new("billing-api-v2", "Billing API v2", "1/2 repos changed · 2 agents working"),
        new("docs-refresh", "Docs refresh", "No changes"),
    ];

    public static PlaceholderWorkspace? Find(string workspaceId) =>
        All.FirstOrDefault(workspace => workspace.Id == workspaceId);
}

internal sealed record PlaceholderWorkspace(string Id, string Name, string Summary);
