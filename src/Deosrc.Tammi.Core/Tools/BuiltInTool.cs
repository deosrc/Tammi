namespace Deosrc.Tammi.Core.Tools;

/// <summary>
/// A built-in tool: one that Tammi knows how to find and start. It's only offered when it's installed (see
/// <see cref="BuiltInToolFinder"/>). The user can disable it or change its scope (see
/// <see cref="BuiltInToolSettings"/>), but not change how it starts.
/// </summary>
/// <param name="Id">Identifies the tool in the settings file. It never changes.</param>
/// <param name="Name">The name shown to the user.</param>
/// <param name="Icon">The icon it's shown with.</param>
/// <param name="Kind">How it opens a folder.</param>
/// <param name="DefaultScope">What it opens, unless the user changes it.</param>
public sealed record BuiltInTool(string Id, string Name, ToolIcon Icon, BuiltInToolKind Kind, ToolScope DefaultScope)
{
    /// <summary>
    /// VS Code.
    /// </summary>
    public static BuiltInTool VsCode { get; } =
        new("vscode", "VS Code", ToolIcon.Editor, BuiltInToolKind.Folder, ToolScope.Both);

    /// <summary>
    /// VS Code, opening the folder in its dev container. It needs VS Code's Dev Containers extension.
    /// </summary>
    public static BuiltInTool DevContainer { get; } =
        new("devcontainer", "Dev Container", ToolIcon.Container, BuiltInToolKind.DevContainer, ToolScope.Both);

    /// <summary>
    /// Visual Studio, on Windows only. It opens solutions.
    /// </summary>
    public static BuiltInTool VisualStudio { get; } =
        new("visual-studio", "Visual Studio", ToolIcon.Ide, BuiltInToolKind.Solution, ToolScope.Both);

    /// <summary>
    /// JetBrains Rider.
    /// </summary>
    public static BuiltInTool Rider { get; } =
        new("rider", "Rider", ToolIcon.Ide, BuiltInToolKind.Folder, ToolScope.Both);

    /// <summary>
    /// The system's file manager.
    /// </summary>
    public static BuiltInTool FileManager { get; } = new(
        "file-manager",
        OperatingSystem.IsWindows() ? "File Explorer" : "File manager",
        ToolIcon.Folder,
        BuiltInToolKind.FileManager,
        ToolScope.Both);

    /// <summary>
    /// The built-in tools for this platform, in the order they're offered.
    /// </summary>
    public static IReadOnlyList<BuiltInTool> All { get; } = OperatingSystem.IsWindows()
        ? [VsCode, DevContainer, VisualStudio, Rider, FileManager]
        : [VsCode, DevContainer, Rider, FileManager];
}
