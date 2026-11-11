namespace Deosrc.Tammi.Core.Settings;

/// <summary>
/// How Home orders sources.
/// </summary>
public enum HomeSourceOrder
{
    /// <summary>
    /// By name, with workspaces that have no task last.
    /// </summary>
    Alphabetical,

    /// <summary>
    /// In the order the user chose. Sources they haven't placed follow, alphabetically.
    /// </summary>
    Custom,
}
