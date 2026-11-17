namespace Deosrc.Tammi.Core.Settings;

/// <summary>
/// The Updates part of the General section of the Settings screen.
/// </summary>
public sealed record UpdateSettings
{
    /// <summary>
    /// Whether Tammi looks for a newer release when it starts, and asks whether to install it.
    /// </summary>
    public bool CheckOnStartup { get; init; } = true;

    /// <summary>
    /// Whether pre-releases (e.g. <c>0.3.0-beta.1</c>) count when looking for a newer release.
    /// </summary>
    public bool IncludePreReleases { get; init; }

    /// <summary>
    /// Nothing needs replacing: every value of both settings is valid.
    /// </summary>
    public UpdateSettings Normalize() => this;
}
