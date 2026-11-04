namespace TAMMi.Core.Integrations;

/// <summary>
/// An integration that provides repositories for the Repository Catalogue.
/// </summary>
public interface IRepositoryIntegration : IIntegration
{
    /// <summary>
    /// Lists the repositories the user can access.
    /// </summary>
    /// <exception cref="IntegrationAuthenticationException">The credentials are missing or were rejected.</exception>
    /// <exception cref="IntegrationException">The service couldn't be reached or returned an error.</exception>
    Task<IReadOnlyList<IntegrationRepository>> GetRepositoriesAsync(CancellationToken cancellationToken = default);
}
