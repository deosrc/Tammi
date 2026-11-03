namespace TAMMi.Core.Integrations;

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
