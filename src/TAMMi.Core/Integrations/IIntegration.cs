namespace TAMMi.Core.Integrations;

/// <summary>
/// A connection to an external service, such as GitHub or JIRA. An integration provides tasks
/// (<see cref="ITaskIntegration"/>), repositories (<see cref="IRepositoryIntegration"/>) or both.
/// </summary>
/// <remarks>
/// TAMMi only reads from integrations. Credentials are kept in the platform secret store through
/// <see cref="IntegrationCredentials"/>, never in settings.
/// </remarks>
public interface IIntegration
{
    /// <summary>
    /// Identifies this configured integration. It is stable across restarts and unique among integrations, because
    /// the user can connect more than one of a kind (e.g. two JIRA sites). Tasks and repositories record it as their
    /// source.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// The name shown to the user, e.g. "GitHub" or "JIRA (acme.atlassian.net)".
    /// </summary>
    string Name { get; }
}

/// <summary>
/// An integration that provides tasks: a Task Source.
/// </summary>
public interface ITaskIntegration : IIntegration
{
    /// <summary>
    /// Finds tasks.
    /// </summary>
    /// <param name="query">
    /// What to search for, in the integration's own syntax (e.g. GitHub search qualifiers or JQL). When
    /// <see langword="null"/> or empty, the integration returns its default list, e.g. the tasks assigned to the user.
    /// </param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="IntegrationAuthenticationException">The credentials are missing or were rejected.</exception>
    /// <exception cref="IntegrationException">The service couldn't be reached or returned an error.</exception>
    Task<IReadOnlyList<TaskInfo>> SearchTasksAsync(string? query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads one task, e.g. to refresh a workspace's task details.
    /// </summary>
    /// <param name="key">The task's <see cref="TaskInfo.Key"/>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The task, or <see langword="null"/> if it doesn't exist or the user can no longer see it.</returns>
    /// <exception cref="IntegrationAuthenticationException">The credentials are missing or were rejected.</exception>
    /// <exception cref="IntegrationException">The service couldn't be reached or returned an error.</exception>
    Task<TaskInfo?> GetTaskAsync(string key, CancellationToken cancellationToken = default);
}

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
