namespace Deosrc.Tammi.Core.Settings;

/// <summary>
/// Which colour theme the app uses.
/// </summary>
public enum ThemePreference
{
    /// <summary>
    /// Follows the system's theme, including when it changes, e.g. at a time of day.
    /// </summary>
    Auto,

    /// <summary>
    /// Always light.
    /// </summary>
    Light,

    /// <summary>
    /// Always dark.
    /// </summary>
    Dark,
}
