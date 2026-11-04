namespace Deosrc.Tammi.UI.About;

/// <summary>
/// A piece of third-party software and its license.
/// </summary>
/// <param name="LicenseFile">The embedded license text, if Tammi ships one.</param>
/// <param name="LicenseUrl">Where to read the license, for licenses only published online.</param>
public sealed record ThirdPartyComponent(
    string Name,
    string Description,
    ThirdPartyPlatforms Platforms,
    string License,
    string ProjectUrl,
    string? LicenseFile = null,
    string? LicenseUrl = null);
