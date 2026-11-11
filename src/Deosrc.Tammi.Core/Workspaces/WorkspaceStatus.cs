using System.Text.Json.Serialization;

namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// The overall status of a workspace's work, as Home groups workspaces by it. This is the workspace's own status, not a
/// Task Status from a task's source.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<WorkspaceStatus>))]
public enum WorkspaceStatus
{
    /// <summary>
    /// Every Pull Request is merged, so the workspace only needs deleting.
    /// </summary>
    Done,

    /// <summary>
    /// Pull Requests are open, and their checks all passed (or there are none).
    /// </summary>
    InReview,

    /// <summary>
    /// A Pull Request has failed checks, an agent needs input or failed, or the workspace can't be read.
    /// </summary>
    RequiresAttention,

    /// <summary>
    /// Changes have been made in the repositories: uncommitted, unpushed, or with an agent at work.
    /// </summary>
    InDevelopment,

    /// <summary>
    /// The workspace exists, but has no changes.
    /// </summary>
    Planning,
}
