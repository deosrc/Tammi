using Deosrc.Tammi.Core.Integrations;

namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// One of a workspace's tasks, with its details as last read from its source.
/// </summary>
/// <param name="Reference">The task, as the workspace's metadata records it.</param>
public sealed record WorkspaceTask(TaskReference Reference)
{
    /// <summary>
    /// Whether the task has been read at least once, whether or not that worked.
    /// </summary>
    public bool IsLoaded { get; init; }

    /// <summary>
    /// The task's details, or <see langword="null"/> until it's read or if it couldn't be.
    /// </summary>
    public TaskInfo? Task { get; init; }

    /// <summary>
    /// Why the task couldn't be read, or <see langword="null"/> if it was (or hasn't been read yet).
    /// </summary>
    public WorkspaceTaskProblem? Problem { get; init; }

    /// <summary>
    /// The error the source gave, when <see cref="Problem"/> is <see cref="WorkspaceTaskProblem.ReadFailed"/>.
    /// </summary>
    public string? ProblemDetail { get; init; }

    /// <summary>
    /// The task's key: the one its source last gave (a moved JIRA issue gets a new key), or the recorded one.
    /// </summary>
    public string Key => Task?.Key ?? Reference.Key;
}
