namespace Deosrc.Tammi.Installation;

/// <summary>
/// A file attached to a release.
/// </summary>
/// <param name="Name">The file's name.</param>
/// <param name="DownloadUrl">Where the file is downloaded from.</param>
/// <param name="Size">The file's size in bytes.</param>
public sealed record ReleaseAsset(string Name, Uri DownloadUrl, long Size);
