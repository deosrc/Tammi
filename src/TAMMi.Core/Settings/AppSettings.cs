namespace TAMMi.Core.Settings;

/// <summary>
/// The app settings, shown on the Settings screen and kept in the settings file (see <see cref="SettingsStore"/>).
/// </summary>
/// <remarks>
/// Each Settings section has its own property, so later sections can be added without changing the others.
/// Settings missing from the file take their default values.
/// </remarks>
public sealed record AppSettings
{
    /// <summary>
    /// The General section.
    /// </summary>
    public GeneralSettings General { get; init; } = new();

    /// <summary>
    /// The Git section.
    /// </summary>
    public GitSettings Git { get; init; } = new();

    /// <summary>
    /// Replaces missing and out-of-range values with valid ones.
    /// </summary>
    public AppSettings Normalize() => this with
    {
        General = (General ?? new GeneralSettings()).Normalize(),
        Git = (Git ?? new GitSettings()).Normalize(),
    };
}

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
    /// Replaces a missing folder with the default.
    /// </summary>
    public GeneralSettings Normalize() => this with
    {
        DefaultWorkspaceParentFolder = string.IsNullOrWhiteSpace(DefaultWorkspaceParentFolder)
            ? DefaultDefaultWorkspaceParentFolder
            : DefaultWorkspaceParentFolder.Trim(),
    };
}

/// <summary>
/// The Git section of the Settings screen.
/// </summary>
public sealed record GitSettings
{
    /// <summary>
    /// The shortest <see cref="StatusRefreshInterval"/>.
    /// </summary>
    public static readonly TimeSpan MinStatusRefreshInterval = TimeSpan.FromMinutes(1);

    /// <summary>
    /// The longest <see cref="StatusRefreshInterval"/>.
    /// </summary>
    public static readonly TimeSpan MaxStatusRefreshInterval = TimeSpan.FromHours(24);

    /// <summary>
    /// The <see cref="StatusRefreshInterval"/> used until the user changes it.
    /// </summary>
    public static readonly TimeSpan DefaultStatusRefreshInterval = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How often the git status of workspaces that aren't open in a tab is refreshed.
    /// </summary>
    public TimeSpan StatusRefreshInterval { get; init; } = DefaultStatusRefreshInterval;

    /// <summary>
    /// Replaces out-of-range values with valid ones.
    /// </summary>
    public GitSettings Normalize() => this with
    {
        StatusRefreshInterval = StatusRefreshInterval < MinStatusRefreshInterval
            ? MinStatusRefreshInterval
            : StatusRefreshInterval > MaxStatusRefreshInterval
                ? MaxStatusRefreshInterval
                : StatusRefreshInterval,
    };
}
