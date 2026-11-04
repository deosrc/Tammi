namespace Deosrc.Tammi.Core.Agents;

/// <summary>
/// The initial prompt that Tammi sends an agent when it starts, telling it about the workspace's task context file.
/// </summary>
/// <remarks>
/// The prompt comes from a template, configured in Settings, with a placeholder for the context file's path. Each
/// Agent Definition says how the prompt is passed to its CLI (see <see cref="AgentDefinition.PromptArguments"/>).
/// </remarks>
public static class InitialPrompt
{
    /// <summary>
    /// The placeholder in a template for the task context file's full path.
    /// </summary>
    public const string ContextFilePlaceholder = "{contextFile}";

    /// <summary>
    /// The start of every predefined template: where the task context file is, and how to get the tasks' latest
    /// details.
    /// </summary>
    private const string ReadContextFile =
        "The tasks for this workspace are described in " + ContextFilePlaceholder + ". Read that file first. "
        + "Tammi rewrites it when the tasks change, so read it again whenever you need their latest details.";

    /// <summary>
    /// A predefined template that has the agent start implementing the tasks.
    /// </summary>
    public const string ImplementTemplate = ReadContextFile + " Then start implementing the tasks.";

    /// <summary>
    /// A predefined template that has the agent plan the change, and wait before making it.
    /// </summary>
    public const string PlanTemplate = ReadContextFile
        + " Then plan the changes the tasks need, and wait for the user to approve the plan before you make them.";

    /// <summary>
    /// A predefined template that has the agent stop once it has read the tasks.
    /// </summary>
    public const string ReadOnlyTemplate = ReadContextFile
        + " Then stop, and wait for the user to tell you what to do.";

    /// <summary>
    /// The template used until the user changes it, and when they clear it.
    /// </summary>
    public const string DefaultTemplate = ReadOnlyTemplate;

    /// <summary>
    /// The templates offered on the Settings screen, in the order they're listed. Each is a single line, so it can be
    /// passed to any agent (see <c>docs/CONTEXT.md</c>).
    /// </summary>
    public static IReadOnlyList<PredefinedPrompt> Predefined { get; } =
    [
        new("Implement the tasks", "Reads the tasks, then starts making the changes.", ImplementTemplate),
        new("Plan the change", "Reads the tasks, then plans the changes and waits for approval.", PlanTemplate),
        new("Read the tasks only", "Reads the tasks, then waits for instructions.", ReadOnlyTemplate),
    ];

    /// <summary>
    /// Fills in a template.
    /// </summary>
    /// <param name="template">The template. Text other than the placeholder is kept as it is.</param>
    /// <param name="contextFilePath">The task context file's path, put in place of every placeholder.</param>
    /// <returns>The prompt.</returns>
    public static string Expand(string template, string contextFilePath)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(contextFilePath);

        return template.Replace(ContextFilePlaceholder, contextFilePath, StringComparison.Ordinal);
    }
}
