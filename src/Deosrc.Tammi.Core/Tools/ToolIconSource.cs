using System.Text.Json.Serialization;

namespace Deosrc.Tammi.Core.Tools;

/// <summary>
/// Where a tool's icon comes from.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ToolIconSource>))]
public enum ToolIconSource
{
    /// <summary>
    /// The icon of the application its executable belongs to, or its generic icon when that can't be found.
    /// </summary>
    Application,

    /// <summary>
    /// One of Tammi's generic icons (see <see cref="ToolIcon"/>).
    /// </summary>
    Generic,

    /// <summary>
    /// An image file chosen by the user, or its generic icon when that can't be read.
    /// </summary>
    Custom,
}
