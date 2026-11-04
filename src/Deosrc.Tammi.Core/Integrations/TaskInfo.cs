namespace TAMMi.Core.Integrations;

/// <summary>
/// A task read from an integration, e.g. a GitHub issue or a JIRA ticket. TAMMi doesn't change tasks.
/// </summary>
/// <param name="Source">The <see cref="IIntegration.Id"/> of the integration the task comes from.</param>
/// <param name="Key">
/// Identifies the task within its source, in the form the user knows it, e.g. <c>acme/api#42</c> or <c>ABC-123</c>.
/// It is used in suggested branch names.
/// </param>
/// <param name="Title">The task's title.</param>
/// <param name="Url">The task's web page.</param>
public sealed record TaskInfo(string Source, string Key, string Title, Uri Url)
{
    /// <summary>
    /// The task's description as the service returns it: markdown from GitHub, wiki markup from JIRA.
    /// <see langword="null"/> when it has none.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// When the task last changed, or <see langword="null"/> if the service didn't say. Tasks from several sources
    /// are listed most recently updated first.
    /// </summary>
    public DateTimeOffset? Updated { get; init; }

    /// <summary>
    /// The web page of the repository the task belongs to (e.g. <c>https://github.com/acme/api</c> for a GitHub
    /// issue), or <see langword="null"/> if it doesn't belong to one. The Create Workspace flow chooses the Local
    /// Repository whose remote matches it.
    /// </summary>
    public Uri? RepositoryUrl { get; init; }

    /// <summary>
    /// The task's status as the service names it, e.g. <c>Open</c> or <c>In Review</c>. <see langword="null"/> if the
    /// service didn't say.
    /// </summary>
    public string? Status { get; init; }

    /// <summary>
    /// What <see cref="Status"/> means for the work, or <see langword="null"/> if the service didn't say.
    /// </summary>
    public TaskStatusCategory? StatusCategory { get; init; }

    /// <summary>
    /// Identifies the task across all integrations.
    /// </summary>
    public TaskReference Reference => new(Source, Key);
}
