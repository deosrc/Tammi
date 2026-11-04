namespace TAMMi.Core.Integrations;

/// <summary>
/// What a <see cref="TaskFinder"/> search looked for, which depends on what the user entered.
/// </summary>
public enum TaskSearchKind
{
    /// <summary>
    /// Nothing was entered: the tasks assigned to the user that are still to do and aren't in a workspace.
    /// </summary>
    Assigned,

    /// <summary>
    /// A link or reference to one task.
    /// </summary>
    Reference,

    /// <summary>
    /// Words, searched in the tasks of the user and their organisations.
    /// </summary>
    Text,
}
