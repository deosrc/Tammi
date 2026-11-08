using Deosrc.Tammi.Core.Tools;

namespace Deosrc.Tammi.UI.Components;

/// <summary>
/// How tools' settings are written on the Settings screen.
/// </summary>
public static class ToolText
{
    /// <summary>
    /// The icons a tool can have, in the order they're offered.
    /// </summary>
    public static IReadOnlyList<ToolIcon> Icons { get; } = Enum.GetValues<ToolIcon>();

    /// <summary>
    /// The scopes a tool can have, in the order they're offered.
    /// </summary>
    public static IReadOnlyList<ToolScope> Scopes { get; } = Enum.GetValues<ToolScope>();

    /// <summary>
    /// An icon's name.
    /// </summary>
    public static string Label(ToolIcon icon) => icon switch
    {
        ToolIcon.Editor => "Code editor",
        ToolIcon.Ide => "IDE",
        ToolIcon.Container => "Container",
        ToolIcon.Folder => "Folder",
        ToolIcon.Terminal => "Terminal",
        _ => "Application",
    };

    /// <summary>
    /// What a scope offers the tool for.
    /// </summary>
    public static string Label(ToolScope scope) => scope switch
    {
        ToolScope.Workspace => "Workspace Folder only",
        ToolScope.Repository => "Repositories only",
        _ => "Workspace Folder and repositories",
    };
}
