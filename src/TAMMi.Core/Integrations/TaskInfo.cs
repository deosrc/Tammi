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
    /// The task's description as the service returns it, usually markdown. <see langword="null"/> when it has none.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Identifies the task across all integrations.
    /// </summary>
    public TaskReference Reference => new(Source, Key);
}

/// <summary>
/// Identifies a task across all integrations, e.g. in a workspace's metadata.
/// </summary>
/// <param name="Source">The <see cref="IIntegration.Id"/> of the integration the task comes from.</param>
/// <param name="Key">The task's <see cref="TaskInfo.Key"/>.</param>
public sealed record TaskReference(string Source, string Key)
{
    public override string ToString() => $"{Source}:{Key}";
}
