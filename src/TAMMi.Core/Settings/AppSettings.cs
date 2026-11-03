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
    /// The Agents section.
    /// </summary>
    public AgentSettings Agents { get; init; } = new();

    /// <summary>
    /// The Tools section.
    /// </summary>
    public ToolSettings Tools { get; init; } = new();

    /// <summary>
    /// Replaces missing and out-of-range values with valid ones.
    /// </summary>
    public AppSettings Normalize() => this with
    {
        General = (General ?? new GeneralSettings()).Normalize(),
        Git = (Git ?? new GitSettings()).Normalize(),
        Agents = (Agents ?? new AgentSettings()).Normalize(),
        Tools = (Tools ?? new ToolSettings()).Normalize(),
    };
}
