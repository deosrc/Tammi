using Deosrc.Tammi.Core.Integrations;

namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// Works out a workspace's source: where its tasks come from, as Home groups workspaces by it.
/// </summary>
public static class WorkspaceSources
{
    /// <summary>
    /// The source of a workspace with no tasks.
    /// </summary>
    public const string NoTasks = "No task";

    /// <summary>
    /// Returns the source of a workspace from its first task: a GitHub issue's repository (e.g. <c>acme/api</c>), or a
    /// JIRA issue's project with its integration (e.g. <c>JIRA (acme.atlassian.net): ABC</c>).
    /// </summary>
    /// <param name="tasks">The workspace's tasks.</param>
    /// <param name="integrationName">Returns an integration's name from its id, or <see langword="null"/>.</param>
    public static string Of(IReadOnlyList<TaskReference> tasks, Func<string, string?> integrationName)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(integrationName);
        if (tasks.Count == 0)
        {
            return NoTasks;
        }

        TaskReference task = tasks[0];
        int hash = task.Key.IndexOf('#', StringComparison.Ordinal);
        if (hash > 0)
        {
            return task.Key[..hash];
        }

        string name = integrationName(task.Source) ?? task.Source;
        int dash = task.Key.LastIndexOf('-');
        return dash > 0 ? $"{name}: {task.Key[..dash]}" : name;
    }
}
