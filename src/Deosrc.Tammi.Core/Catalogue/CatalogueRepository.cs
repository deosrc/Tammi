using Deosrc.Tammi.Core.Git;

namespace Deosrc.Tammi.Core.Catalogue;

/// <summary>
/// A repository in the Repository Catalogue.
/// </summary>
/// <remarks>
/// Only Local Repositories from local sources are catalogued so far: those in Monitored Folders and those added
/// manually. A repository found by both is listed once, with both sources.
/// </remarks>
public sealed record CatalogueRepository
{
    /// <summary>
    /// The full path of the Local Repository's working directory.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// The repository's name: its folder's name, e.g. <c>api</c>.
    /// </summary>
    public string Name => System.IO.Path.GetFileName(Path);

    /// <summary>
    /// The repository's remotes, in name order. A repository can have none.
    /// </summary>
    public IReadOnlyList<GitRemote> Remotes { get; init; } = [];

    /// <summary>
    /// Whether the repository was added manually, in the Git settings.
    /// </summary>
    public bool IsManuallyAdded { get; init; }

    /// <summary>
    /// The Monitored Folders the repository was found in. It is usually one, but Monitored Folders can overlap.
    /// </summary>
    public IReadOnlyList<string> MonitoredFolders { get; init; } = [];
}
