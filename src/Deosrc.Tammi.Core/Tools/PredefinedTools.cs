namespace Deosrc.Tammi.Core.Tools;

/// <summary>
/// The predefined tools: tool definitions that come with Tammi. The user can enable or disable them (except
/// <see cref="Terminal"/>), but not change or remove them. Like any tool, each one is only offered when its executable is
/// found.
/// </summary>
public static class PredefinedTools
{
    private static readonly string PreferredFolder = ToolPlaceholders.Format(ToolPlaceholders.PreferredFolder);

    /// <summary>
    /// One of Tammi's own terminals, opened in a tab of the Workspace View with the terminal shell from the settings, as
    /// the New menu's Terminal does, in the <see cref="ToolPlaceholders.PreferredFolder"/>. It has no executable, can't
    /// be disabled, and is always offered first.
    /// </summary>
    public static ToolDefinition Terminal { get; } = new()
    {
        Id = "terminal",
        Name = "Terminal",
        OpensTerminal = true,
        Icon = new ToolIconSettings { Source = ToolIconSource.Generic, Generic = ToolIcon.Terminal },
    };

    /// <summary>
    /// VS Code, opening the folder.
    /// </summary>
    public static ToolDefinition VsCode { get; } = new()
    {
        Id = "vscode",
        Name = "VS Code",
        Executable = "code",
        Arguments = [PreferredFolder],
        Icon = new ToolIconSettings { Generic = ToolIcon.Editor },
    };

    /// <summary>
    /// VS Code, opening a repository in its dev container. It uses the <c>devcontainer</c> CLI that VS Code's Dev
    /// Containers extension installs (with its "Install devcontainer CLI" command), so it's only offered once that's
    /// installed, and only for repositories with a dev container configuration.
    /// </summary>
    public static ToolDefinition DevContainer { get; } = new()
    {
        Id = "devcontainer",
        Kind = ToolKind.File,
        Name = "Dev Container",
        FilePatterns = [".devcontainer/devcontainer.json", ".devcontainer.json"],
        Executable = "devcontainer",
        Arguments = ["open", ToolPlaceholders.Format(ToolPlaceholders.RepositoryFolder)],
        Scope = ToolScope.Repository,
        Icon = new ToolIconSettings { Generic = ToolIcon.Container },
    };

    /// <summary>
    /// Visual Studio, on Windows only, opening each solution and solution filter. It's started through the Visual
    /// Studio Version Selector, which opens each solution in the version of Visual Studio it was made for.
    /// </summary>
    public static ToolDefinition VisualStudio { get; } = new()
    {
        Id = "visual-studio",
        Kind = ToolKind.File,
        Name = "Visual Studio - " + ToolPlaceholders.Format(ToolPlaceholders.FileName),
        FilePatterns = ["*.sln", "*.slnf", "*.slnx"],
        Executable = @"%CommonProgramFiles(x86)%\Microsoft Shared\MSEnv\VSLauncher.exe",
        Arguments = [ToolPlaceholders.Format(ToolPlaceholders.FilePath)],
        Icon = new ToolIconSettings { Generic = ToolIcon.Ide },
    };

    /// <summary>
    /// JetBrains Rider, opening the folder. It's found as <c>rider</c> on the search path, e.g. from JetBrains
    /// Toolbox's scripts folder.
    /// </summary>
    public static ToolDefinition Rider { get; } = new()
    {
        Id = "rider",
        Name = "Rider",
        Executable = "rider",
        Arguments = [PreferredFolder],
        Icon = new ToolIconSettings { Generic = ToolIcon.Ide },
    };

    /// <summary>
    /// The system's file manager, opening the folder: File Explorer on Windows, and the default file manager (through
    /// <c>xdg-open</c>) on Linux.
    /// </summary>
    public static ToolDefinition FileManager { get; } = OperatingSystem.IsWindows()
        ? new()
        {
            Id = "file-manager",
            Name = "File Explorer",
            Executable = "explorer",
            Arguments = [PreferredFolder],
            Icon = new ToolIconSettings { Generic = ToolIcon.Folder },
        }
        : new()
        {
            Id = "file-manager",
            Name = "File manager",
            Executable = "xdg-open",
            Arguments = [PreferredFolder],
            Icon = new ToolIconSettings { Generic = ToolIcon.Folder },
        };

    /// <summary>
    /// The predefined tools for this platform, in the order they're offered: <see cref="Terminal"/> first.
    /// </summary>
    public static IReadOnlyList<ToolDefinition> All { get; } = OperatingSystem.IsWindows()
        ? [Terminal, VsCode, DevContainer, VisualStudio, Rider, FileManager]
        : [Terminal, VsCode, DevContainer, Rider, FileManager];
}
