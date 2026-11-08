using System.Text.Json.Serialization;

namespace Deosrc.Tammi.Core.Tools;

/// <summary>
/// What a tool opens for a workspace with only one repository, when it's launched for the workspace: the folder that
/// <see cref="ToolPlaceholders.PreferredFolder"/> is.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<SingleRepositoryTarget>))]
public enum SingleRepositoryTarget
{
    /// <summary>
    /// The repository, since it's all the workspace has.
    /// </summary>
    Repository,

    /// <summary>
    /// The Workspace Folder, as for a workspace with several repositories.
    /// </summary>
    Workspace,
}
