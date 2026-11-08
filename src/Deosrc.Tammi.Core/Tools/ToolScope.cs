using System.Text.Json.Serialization;

namespace Deosrc.Tammi.Core.Tools;

/// <summary>
/// What a tool can open. Workspace tools are offered in the side panel's Tools section, and repository tools on each
/// repository's card in the Repositories section.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ToolScope>))]
public enum ToolScope
{
    /// <summary>
    /// The Workspace Folder and each repository.
    /// </summary>
    Both,

    /// <summary>
    /// Only the Workspace Folder.
    /// </summary>
    Workspace,

    /// <summary>
    /// Only a single repository.
    /// </summary>
    Repository,
}
