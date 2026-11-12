using Deosrc.Tammi.Core.Credentials;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Deosrc.Tammi.Core.Integrations;

/// <summary>
/// Finds tasks in every connected Task Source at once, for the Create Workspace flow. The user enters one thing and
/// the finder decides what it is:
/// <list type="bullet">
/// <item>Nothing: the tasks assigned to the user that are still to do, leaving out those already in a workspace.</item>
/// <item>
/// A link or reference that a source recognises (e.g. an issue's URL, <c>owner/repo#42</c> or <c>ABC-123</c>): that
/// task, from each source that recognises it. This is how public tasks outside the user's organisations are found.
/// </item>
/// <item>Any other text: a search of the text of the tasks of the user and their organisations.</item>
/// </list>
/// </summary>
/// <remarks>
/// The sources are searched at the same time and their results combined, most recently updated first. A source that
/// fails doesn't stop the others: its error is returned with their results.
/// </remarks>
public sealed class TaskFinder
{
    private readonly IntegrationRegistry _registry;
    private readonly ILogger _logger;

    /// <param name="registry">The connected integrations.</param>
    /// <param name="logger">Logs the sources that fail.</param>
    public TaskFinder(IntegrationRegistry registry, ILogger<TaskFinder>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
        _logger = logger ?? NullLogger<TaskFinder>.Instance;
    }

    /// <summary>
    /// Finds tasks.
    /// </summary>
    /// <param name="text">What the user entered: nothing, a link or reference, or words to search for.</param>
    /// <param name="inWorkspaces">
    /// The tasks already in a workspace. They're left out of the assigned tasks, but still found by a search or link.
    /// </param>
    /// <param name="cancellationToken">Cancels the search.</param>
    public async Task<TaskSearchResults> FindAsync(
        string? text,
        IReadOnlySet<TaskReference> inWorkspaces,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inWorkspaces);
        IReadOnlyList<ITaskIntegration> sources = _registry.TaskIntegrations;
        text = text?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            TaskSearchResults assigned = await SearchAllAsync(
                TaskSearchKind.Assigned,
                sources.Select(source => (source, (Func<Task<IReadOnlyList<TaskInfo>>>)(
                    () => source.GetAssignedTasksAsync(cancellationToken)))),
                cancellationToken).ConfigureAwait(false);
            return assigned with
            {
                Tasks = [.. assigned.Tasks.Where(task => !inWorkspaces.Contains(task.Reference))],
            };
        }

        var references = new List<(ITaskIntegration Source, string Key)>();
        foreach (ITaskIntegration source in sources)
        {
            if (source.TryParseTaskReference(text, out string? key))
            {
                references.Add((source, key));
            }
        }

        if (references.Count > 0)
        {
            return await SearchAllAsync(
                TaskSearchKind.Reference,
                references.Select(reference => (reference.Source, (Func<Task<IReadOnlyList<TaskInfo>>>)(
                    async () => await reference.Source.GetTaskAsync(reference.Key, cancellationToken)
                        .ConfigureAwait(false) is { } task ? [task] : []))),
                cancellationToken).ConfigureAwait(false);
        }

        // A link that no source recognises isn't searched as text: it would only find tasks that mention it.
        if (IsLink(text))
        {
            return new TaskSearchResults(TaskSearchKind.Reference, [], []) { IsUnrecognisedLink = true };
        }

        // Shorthands (e.g. `repo#6`, `repo some words`, `1234`) are found as well as, and listed before, whatever the
        // text matches: a number or a repository's name can also appear in the titles of other tasks.
        Task<TaskSearchResults> shorthandSearch = SearchAllAsync(
            TaskSearchKind.Text,
            sources.Select(source => (source, (Func<Task<IReadOnlyList<TaskInfo>>>)(
                () => source.FindByShorthandAsync(text, cancellationToken)))),
            cancellationToken);
        Task<TaskSearchResults> textSearch = SearchAllAsync(
            TaskSearchKind.Text,
            sources.Select(source => (source, (Func<Task<IReadOnlyList<TaskInfo>>>)(
                () => source.SearchTasksAsync(text, cancellationToken)))),
            cancellationToken);
        TaskSearchResults shorthand = await shorthandSearch.ConfigureAwait(false);
        TaskSearchResults search = await textSearch.ConfigureAwait(false);

        var seen = new HashSet<TaskReference>(shorthand.Tasks.Select(task => task.Reference));
        return search with
        {
            Tasks = [.. shorthand.Tasks, .. search.Tasks.Where(task => !seen.Contains(task.Reference))],
            Errors = [.. search.Errors.UnionBy(shorthand.Errors, error => error.Source)],
        };
    }

    private static bool IsLink(string text) =>
        Uri.TryCreate(text, UriKind.Absolute, out Uri? url) && url.Scheme is "https" or "http";

    private async Task<TaskSearchResults> SearchAllAsync(
        TaskSearchKind kind,
        IEnumerable<(ITaskIntegration Source, Func<Task<IReadOnlyList<TaskInfo>>> Search)> searches,
        CancellationToken cancellationToken)
    {
        var outcomes = await Task.WhenAll(searches.Select(async search =>
        {
            try
            {
                return (Tasks: await search.Search().ConfigureAwait(false), Error: (TaskSourceError?)null);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is IntegrationException or CredentialStoreException)
            {
                _logger.LogWarning(
                    "Couldn't get {SearchKind} tasks from {Source}: {Message}", kind, search.Source.Id, ex.Message);
                return (Tasks: (IReadOnlyList<TaskInfo>)[], Error: new TaskSourceError(
                    search.Source.Id,
                    search.Source.Name,
                    ex.Message,
                    ex is IntegrationAuthenticationException));
            }
        })).ConfigureAwait(false);

        var seen = new HashSet<TaskReference>();
        TaskInfo[] tasks =
        [
            .. outcomes
                .SelectMany(outcome => outcome.Tasks)
                .Where(task => seen.Add(task.Reference))
                // Stable, so tasks updated at the same time (or never, as far as their source says) keep their
                // source's order.
                .OrderByDescending(task => task.Updated ?? DateTimeOffset.MinValue),
        ];
        return new TaskSearchResults(kind, tasks, [.. outcomes.Select(outcome => outcome.Error).OfType<TaskSourceError>()]);
    }
}
