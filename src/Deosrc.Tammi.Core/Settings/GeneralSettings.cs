using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Deosrc.Tammi.Core.Logging;

namespace Deosrc.Tammi.Core.Settings;

/// <summary>
/// The General section of the Settings screen.
/// </summary>
public sealed record GeneralSettings
{
    /// <summary>
    /// The <see cref="DefaultWorkspaceParentFolder"/> used until the user changes it: <c>Workspaces</c> in the
    /// user's home folder.
    /// </summary>
    public static string DefaultDefaultWorkspaceParentFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Workspaces");

    /// <summary>
    /// The folder new workspaces are created in, unless the user picks another one.
    /// </summary>
    public string DefaultWorkspaceParentFolder { get; init; } = DefaultDefaultWorkspaceParentFolder;

    /// <summary>
    /// How the Workspace Folder's name suggested from a task is written.
    /// </summary>
    public WorkspaceFolderNameFormat WorkspaceFolderNameFormat { get; init; } = new();

    /// <summary>
    /// The template the workspace's name is suggested from, with placeholders for the task's details (see
    /// <c>WorkspaceNames</c>). When it can't be filled in, e.g. the workspace has several tasks, the name is the
    /// Workspace Folder's name. A blank template always uses the folder's name.
    /// </summary>
    public string WorkspaceNameTemplate { get; init; } = DefaultWorkspaceNameTemplate;

    /// <summary>
    /// The <see cref="WorkspaceNameTemplate"/> used until the user changes it: the task's reference and title, e.g.
    /// <c>ABC-123 Add some feature</c>.
    /// </summary>
    public const string DefaultWorkspaceNameTemplate = "{TaskReference} {TaskTitle}";

    /// <summary>
    /// The lowest level Tammi logs at.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<LogLevel>))]
    public LogLevel LogLevel { get; init; } = LogLevelSwitch.DefaultLevel;

    /// <summary>
    /// Replaces a missing folder, folder name format or name template, or an unknown log level, with the default.
    /// </summary>
    public GeneralSettings Normalize() => this with
    {
        LogLevel = Enum.IsDefined(LogLevel) && LogLevel != LogLevel.None ? LogLevel : LogLevelSwitch.DefaultLevel,
        WorkspaceFolderNameFormat = (WorkspaceFolderNameFormat ?? new WorkspaceFolderNameFormat()).Normalize(),
        WorkspaceNameTemplate = WorkspaceNameTemplate ?? DefaultWorkspaceNameTemplate,
        DefaultWorkspaceParentFolder = string.IsNullOrWhiteSpace(DefaultWorkspaceParentFolder)
            ? DefaultDefaultWorkspaceParentFolder
            : DefaultWorkspaceParentFolder.Trim(),
    };
}
