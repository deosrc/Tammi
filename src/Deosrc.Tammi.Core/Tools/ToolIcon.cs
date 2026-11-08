using System.Text.Json.Serialization;

namespace Deosrc.Tammi.Core.Tools;

/// <summary>
/// The icon a tool is shown with.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ToolIcon>))]
public enum ToolIcon
{
    /// <summary>
    /// Any application.
    /// </summary>
    Application,

    /// <summary>
    /// A code editor, e.g. VS Code.
    /// </summary>
    Editor,

    /// <summary>
    /// An IDE, e.g. Rider or Visual Studio.
    /// </summary>
    Ide,

    /// <summary>
    /// A container, e.g. a dev container.
    /// </summary>
    Container,

    /// <summary>
    /// A folder, e.g. the file manager.
    /// </summary>
    Folder,

    /// <summary>
    /// A terminal.
    /// </summary>
    Terminal,
}
