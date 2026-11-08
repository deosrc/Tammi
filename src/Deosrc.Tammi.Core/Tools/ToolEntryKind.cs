namespace Deosrc.Tammi.Core.Tools;

/// <summary>
/// How a tool entry opens its folder or solution.
/// </summary>
public enum ToolEntryKind
{
    /// <summary>
    /// Its <see cref="ToolEntry.Request"/> is started, and keeps running after Tammi exits.
    /// </summary>
    Process,

    /// <summary>
    /// Its <see cref="ToolEntry.Path"/> is opened with the system's default application, i.e. the file manager.
    /// </summary>
    SystemDefault,
}
