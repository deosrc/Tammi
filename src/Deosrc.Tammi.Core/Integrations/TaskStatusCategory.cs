namespace Deosrc.Tammi.Core.Integrations;

/// <summary>
/// What a task's status means for the work, whatever the Task Source calls it. JIRA puts every status, including ones
/// a site defines, in one of these categories. Sources with fixed statuses map theirs to the nearest one, e.g. an open
/// GitHub issue is <see cref="ToDo"/> and a closed one <see cref="Done"/>.
/// </summary>
public enum TaskStatusCategory
{
    /// <summary>
    /// The work hasn't started.
    /// </summary>
    ToDo,

    /// <summary>
    /// The work has started.
    /// </summary>
    InProgress,

    /// <summary>
    /// The work is finished, or won't be done.
    /// </summary>
    Done,
}
