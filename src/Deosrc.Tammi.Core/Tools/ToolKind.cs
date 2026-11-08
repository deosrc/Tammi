using System.Text.Json.Serialization;

namespace Deosrc.Tammi.Core.Tools;

/// <summary>
/// What a tool opens.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ToolKind>))]
public enum ToolKind
{
    /// <summary>
    /// A folder: the Workspace Folder or a repository, e.g. VS Code or the file manager. It has one entry for each
    /// folder its scope includes.
    /// </summary>
    Folder,

    /// <summary>
    /// A file found in the repositories by its <see cref="ToolDefinition.FilePatterns"/>, e.g. Visual Studio for
    /// solutions. It has an entry for each file found, and none when there aren't any.
    /// </summary>
    File,
}
