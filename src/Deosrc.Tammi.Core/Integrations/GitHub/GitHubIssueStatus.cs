namespace Deosrc.Tammi.Core.Integrations.GitHub;

/// <summary>
/// An issue's status, in repositories that have GitHub's issue statuses (in preview, so not every repository has them
/// yet). Closed issues match the status for why they were closed.
/// </summary>
/// <param name="Id">The status's ID.</param>
/// <param name="Name">The status's name, e.g. <c>Todo</c> or <c>In review</c>.</param>
/// <param name="Category">
/// <c>backlog</c> (Backlog), <c>planned</c> (Todo), <c>started</c> (In progress, In review), <c>completed</c> (Done),
/// <c>not-planned</c> (Not planned) or <c>duplicate</c> (Duplicate). GitHub may add more.
/// </param>
internal sealed record GitHubIssueStatus(long Id, string Name, string? Category)
{
    /// <summary>
    /// <see cref="Category"/> as a <see cref="TaskStatusCategory"/>, or <see langword="null"/> if it's not one Tammi
    /// knows.
    /// </summary>
    public TaskStatusCategory? TaskCategory => Category switch
    {
        "backlog" or "planned" => TaskStatusCategory.ToDo,
        "started" => TaskStatusCategory.InProgress,
        "completed" or "not-planned" or "duplicate" => TaskStatusCategory.Done,
        _ => null,
    };
}
