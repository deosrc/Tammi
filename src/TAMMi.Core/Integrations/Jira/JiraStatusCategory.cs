namespace TAMMi.Core.Integrations.Jira;

/// <summary>
/// The category of an issue's status, from REST API version 2.
/// </summary>
/// <param name="Key">
/// <c>new</c> (To Do), <c>indeterminate</c> (In Progress) or <c>done</c>. A status JIRA hasn't categorised has
/// <c>undefined</c>.
/// </param>
internal sealed record JiraStatusCategory(string? Key)
{
    /// <summary>
    /// <see cref="Key"/> as a <see cref="TaskStatusCategory"/>, or <see langword="null"/> if it's not one TAMMi knows.
    /// </summary>
    public TaskStatusCategory? Category => Key switch
    {
        "new" => TaskStatusCategory.ToDo,
        "indeterminate" => TaskStatusCategory.InProgress,
        "done" => TaskStatusCategory.Done,
        _ => null,
    };
}
