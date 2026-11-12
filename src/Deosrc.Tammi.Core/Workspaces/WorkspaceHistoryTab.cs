namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// The History tab of a workspace's Workspace View: its past Agent Sessions, which can be resumed or forgotten. Open and
/// close it with <see cref="WorkspaceHistoryTabs"/>.
/// </summary>
public sealed class WorkspaceHistoryTab : IMainAreaTab
{
    internal WorkspaceHistoryTab(string workspaceId)
    {
        WorkspaceId = workspaceId;
        StartOrder = MainAreaTabOrder.Next();
    }

    /// <inheritdoc />
    public string WorkspaceId { get; }

    /// <summary>
    /// The tab's name.
    /// </summary>
    public string Name => "History";

    /// <inheritdoc />
    public long StartOrder { get; }
}
