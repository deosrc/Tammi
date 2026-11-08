using Deosrc.Tammi.Core.Settings;
using Deosrc.Tammi.Core.Tools;

namespace Deosrc.Tammi.UI.Components;

/// <summary>
/// How tools' settings are written on the Settings screen.
/// </summary>
public static class ToolText
{
    /// <summary>
    /// The generic icons a tool can have, in the order they're offered.
    /// </summary>
    public static IReadOnlyList<ToolIcon> Icons { get; } = Enum.GetValues<ToolIcon>();

    /// <summary>
    /// The scopes a tool can have, in the order they're offered.
    /// </summary>
    public static IReadOnlyList<ToolScope> Scopes { get; } = Enum.GetValues<ToolScope>();

    /// <summary>
    /// The kinds of tool, in the order they're offered.
    /// </summary>
    public static IReadOnlyList<ToolKind> Kinds { get; } = Enum.GetValues<ToolKind>();

    /// <summary>
    /// A generic icon's name.
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

    /// <summary>
    /// What a kind of tool opens.
    /// </summary>
    public static string Label(ToolKind kind) => kind switch
    {
        ToolKind.File => "Files",
        _ => "Folders",
    };

    /// <summary>
    /// What a placeholder is replaced with.
    /// </summary>
    public static string Description(string placeholder) => placeholder switch
    {
        ToolPlaceholders.PreferredFolder =>
            "the folder the tool should open: as {LaunchFolder}, but the repository for a workspace with only one "
            + "repository, if that's chosen above",
        ToolPlaceholders.LaunchFolder => "the folder the tool is offered for: the Workspace Folder or the repository",
        ToolPlaceholders.WorkspaceFolder => "the Workspace Folder",
        ToolPlaceholders.RepositoryFolder =>
            "the repository's folder (only for repositories, or the repository the file is in)",
        ToolPlaceholders.FileDirectory => "the folder the file is in",
        ToolPlaceholders.FileName => "the file's name, e.g. Api.sln",
        ToolPlaceholders.FileNameWithoutExtension => "the file's name without its extension, e.g. Api",
        ToolPlaceholders.FilePath => "the file's full path",
        ToolPlaceholders.MatchFolder =>
            "the folder the file pattern matched from: the file's folder for a file name, or where a path pattern "
            + "starts, e.g. the folder holding .devcontainer/",
        _ => placeholder,
    };

    /// <summary>
    /// The placeholders in a tool's name and arguments that it can't use, because they're only for file tools.
    /// </summary>
    public static IReadOnlyList<string> UnavailablePlaceholders(ToolDefinition tool) =>
        Templates(tool).SelectMany(ToolPlaceholders.FindUsed)
            .Except(ToolPlaceholders.For(tool.Kind))
            .Distinct()
            .ToArray();

    /// <summary>
    /// The text in braces in a tool's name and arguments that isn't a placeholder.
    /// </summary>
    public static IReadOnlyList<string> UnknownPlaceholders(ToolDefinition tool) =>
        Templates(tool).SelectMany(ToolPlaceholders.FindUnknown).Distinct().ToArray();

    private static IEnumerable<string> Templates(ToolDefinition tool) => [tool.Name, .. tool.Arguments];
}
