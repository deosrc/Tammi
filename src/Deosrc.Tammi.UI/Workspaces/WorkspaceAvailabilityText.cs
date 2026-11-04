using Deosrc.Tammi.Core.Workspaces;

namespace Deosrc.Tammi.UI.Workspaces;

/// <summary>
/// Short descriptions of why a workspace isn't available, short enough to fit on one line of a Home card. The full
/// reason is in <see cref="WorkspaceListing.Problem"/>.
/// </summary>
internal static class WorkspaceAvailabilityText
{
    public static string Summary(WorkspaceAvailability availability) => availability switch
    {
        WorkspaceAvailability.Missing => "Workspace folder missing",
        WorkspaceAvailability.Unreadable => "Corrupt",
        _ => "Available",
    };
}
