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
    /// How the workspace name suggested from a task is written.
    /// </summary>
    public WorkspaceNameFormat WorkspaceNameFormat { get; init; } = new();

    /// <summary>
    /// Replaces a missing folder or name format with the default.
    /// </summary>
    public GeneralSettings Normalize() => this with
    {
        WorkspaceNameFormat = (WorkspaceNameFormat ?? new WorkspaceNameFormat()).Normalize(),
        DefaultWorkspaceParentFolder = string.IsNullOrWhiteSpace(DefaultWorkspaceParentFolder)
            ? DefaultDefaultWorkspaceParentFolder
            : DefaultWorkspaceParentFolder.Trim(),
    };
}
