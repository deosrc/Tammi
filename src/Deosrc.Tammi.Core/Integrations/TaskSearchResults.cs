namespace TAMMi.Core.Integrations;

/// <summary>
/// The tasks a <see cref="TaskFinder"/> search found in all the Task Sources.
/// </summary>
/// <param name="Kind">What was searched for.</param>
/// <param name="Tasks">The tasks, most recently updated first.</param>
/// <param name="Errors">The sources that couldn't be searched.</param>
public sealed record TaskSearchResults(
    TaskSearchKind Kind,
    IReadOnlyList<TaskInfo> Tasks,
    IReadOnlyList<TaskSourceError> Errors)
{
    /// <summary>
    /// Whether the search was for a link that no connected Task Source recognises, so nothing was searched.
    /// </summary>
    public bool IsUnrecognisedLink { get; init; }
}
