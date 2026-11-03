namespace TAMMi.Core.Settings;

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
