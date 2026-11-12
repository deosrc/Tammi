using System.Diagnostics.CodeAnalysis;

namespace Deosrc.Tammi.Core.Integrations;

/// <summary>
/// An integration that provides tasks: a Task Source.
/// </summary>
/// <remarks>
/// The Create Workspace flow searches every Task Source at once (see <see cref="TaskFinder"/>), so the ways of finding
/// tasks are the same for every source: the tasks assigned to the user, a search by text, and a link or reference to
/// one task.
/// </remarks>
public interface ITaskIntegration : IIntegration
{
    /// <summary>
    /// Lists the tasks assigned to the user that are still to do: not done, and not in progress where the service
    /// tracks that. They are most recently updated first.
    /// </summary>
    /// <exception cref="IntegrationAuthenticationException">The credentials are missing or were rejected.</exception>
    /// <exception cref="IntegrationException">The service couldn't be reached or returned an error.</exception>
    Task<IReadOnlyList<TaskInfo>> GetAssignedTasksAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches the text of the tasks that belong to the user and their organisations (or the service's equivalent,
    /// e.g. a JIRA site). Public tasks elsewhere aren't searched: find those by link or reference.
    /// </summary>
    /// <param name="text">The words to look for.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="ArgumentException"><paramref name="text"/> is blank.</exception>
    /// <exception cref="IntegrationAuthenticationException">The credentials are missing or were rejected.</exception>
    /// <exception cref="IntegrationException">The service couldn't be reached or returned an error.</exception>
    Task<IReadOnlyList<TaskInfo>> SearchTasksAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds tasks that a shorthand refers to, which <see cref="TryParseTaskReference"/> doesn't read because it takes
    /// the user's own repositories, projects or similar to complete it (e.g. <c>repo#42</c> without the owner,
    /// <c>repo some words</c>, or <c>42</c> without the project key). Tasks that match the text in other ways are
    /// still found by <see cref="SearchTasksAsync"/>, so this only adds to those.
    /// </summary>
    /// <param name="text">What the user entered.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The tasks the shorthand could mean, or none if <paramref name="text"/> isn't one of its shorthands.</returns>
    /// <exception cref="IntegrationAuthenticationException">The credentials are missing or were rejected.</exception>
    /// <exception cref="IntegrationException">The service couldn't be reached or returned an error.</exception>
    Task<IReadOnlyList<TaskInfo>> FindByShorthandAsync(
        string text,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TaskInfo>>([]);

    /// <summary>
    /// Reads a task's link (its web page on this integration's service) or reference in the service's own form
    /// (e.g. <c>owner/repo#42</c> or <c>ABC-123</c>). It doesn't contact the service.
    /// </summary>
    /// <param name="text">The link or reference.</param>
    /// <param name="key">The task's <see cref="TaskInfo.Key"/>, to pass to <see cref="GetTaskAsync"/>.</param>
    /// <returns>Whether <paramref name="text"/> is a link or reference to a task of this integration.</returns>
    bool TryParseTaskReference(string? text, [NotNullWhen(true)] out string? key);

    /// <summary>
    /// Reads one task, e.g. one the user linked to, or to refresh a workspace's task details.
    /// </summary>
    /// <param name="key">The task's <see cref="TaskInfo.Key"/>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The task, or <see langword="null"/> if it doesn't exist or the user can't see it.</returns>
    /// <exception cref="IntegrationAuthenticationException">The credentials are missing or were rejected.</exception>
    /// <exception cref="IntegrationException">The service couldn't be reached or returned an error.</exception>
    Task<TaskInfo?> GetTaskAsync(string key, CancellationToken cancellationToken = default);
}
