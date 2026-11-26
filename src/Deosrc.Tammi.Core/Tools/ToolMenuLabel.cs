using System.Text.Json.Serialization;

namespace Deosrc.Tammi.Core.Tools;

/// <summary>
/// What a file tool's menu item for a file is called, so the files it found can be told apart.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ToolMenuLabel>))]
public enum ToolMenuLabel
{
    /// <summary>
    /// The file's name, e.g. <c>Api.sln</c>.
    /// </summary>
    FileName,

    /// <summary>
    /// The file's name without its extension, e.g. <c>Api</c>.
    /// </summary>
    FileNameWithoutExtension,

    /// <summary>
    /// The name of the folder the tool starts in: the file's folder for a file name pattern, or e.g. the folder holding
    /// <c>.devcontainer/</c> for a path pattern.
    /// </summary>
    LaunchFolder,

    /// <summary>
    /// The file's full path.
    /// </summary>
    FullPath,
}
