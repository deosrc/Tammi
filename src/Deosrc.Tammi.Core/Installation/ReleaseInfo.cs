namespace Deosrc.Tammi.Core.Installation;

/// <summary>
/// A published Tammi release.
/// </summary>
/// <param name="Tag">The release's tag, e.g. <c>v0.2.0</c>.</param>
/// <param name="Version">The version the tag names.</param>
/// <param name="Url">The release's page.</param>
/// <param name="Assets">The files attached to the release.</param>
/// <param name="Name">The release's title, or <see langword="null"/> if it has none.</param>
/// <param name="Notes">The release notes, as markdown, or <see langword="null"/> if there are none.</param>
public sealed record ReleaseInfo(
    string Tag, PackageVersion Version, Uri Url, IReadOnlyList<ReleaseAsset> Assets, string? Name = null, string? Notes = null)
{
    /// <summary>
    /// The title to show: the release's name, or its tag if it has no name.
    /// </summary>
    public string Title => string.IsNullOrWhiteSpace(Name) ? Tag : Name;

    /// <summary>
    /// Whether this is a pre-release.
    /// </summary>
    public bool IsPreRelease => Version.IsPreRelease;

    /// <summary>
    /// Finds the archive for a target. Archives are named <c>tammi-{tag}-fd-{rid}</c> with a <c>.zip</c> (Windows)
    /// or <c>.tar.gz</c> extension. <c>fd</c> is for framework-dependent: the .NET runtime must be installed. Releases
    /// don't have self-contained builds at the moment (<c>sc</c>).
    /// </summary>
    /// <returns>The archive, or <see langword="null"/> if the release has none for the target.</returns>
    public ReleaseAsset? FindArchive(ReleaseTarget target)
    {
        var prefix = $"tammi-{Tag}-fd-{target.RuntimeIdentifier}.";
        return Assets.FirstOrDefault(asset =>
            asset.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && (asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                || asset.Name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase)));
    }
}
