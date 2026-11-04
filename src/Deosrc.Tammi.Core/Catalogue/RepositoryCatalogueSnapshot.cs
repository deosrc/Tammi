namespace Deosrc.Tammi.Core.Catalogue;

/// <summary>
/// The Repository Catalogue as it was at the end of a scan.
/// </summary>
public sealed record RepositoryCatalogueSnapshot
{
    /// <summary>
    /// The catalogue before its first scan.
    /// </summary>
    public static RepositoryCatalogueSnapshot Empty { get; } = new();

    /// <summary>
    /// The catalogue's repositories, in name order, then path order.
    /// </summary>
    public IReadOnlyList<CatalogueRepository> Repositories { get; init; } = [];

    /// <summary>
    /// The sources that couldn't be catalogued, in the order they're in the settings.
    /// </summary>
    public IReadOnlyList<CatalogueProblem> Problems { get; init; } = [];

    /// <summary>
    /// When the scan finished, or <c>null</c> before the first scan.
    /// </summary>
    public DateTimeOffset? ScannedAt { get; init; }
}
