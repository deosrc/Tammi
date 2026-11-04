namespace TAMMi.Core.Integrations;

/// <summary>
/// A repository known through an integration, e.g. a GitHub repository the user can access. It becomes a Local
/// Repository in the Repository Catalogue when a local clone has a remote matching one of its
/// <see cref="CloneUrls"/>, and is otherwise a Remote-only Repository.
/// </summary>
/// <param name="Source">The <see cref="IIntegration.Id"/> of the integration the repository comes from.</param>
/// <param name="Owner">The user or organisation that owns the repository, e.g. <c>acme</c>.</param>
/// <param name="Name">The repository's name, e.g. <c>api</c>.</param>
/// <param name="CloneUrls">
/// The URLs the repository can be cloned from, preferred first, e.g. its HTTPS and SSH URLs. SSH URLs such as
/// <c>git@github.com:acme/api.git</c> aren't valid <see cref="Uri"/>s, so these are strings.
/// </param>
public sealed record IntegrationRepository(
    string Source,
    string Owner,
    string Name,
    IReadOnlyList<string> CloneUrls)
{
    /// <summary>
    /// The repository's web page, if the service has one.
    /// </summary>
    public Uri? Url { get; init; }

    /// <summary>
    /// The owner and name, e.g. <c>acme/api</c>.
    /// </summary>
    public string FullName => $"{Owner}/{Name}";
}
