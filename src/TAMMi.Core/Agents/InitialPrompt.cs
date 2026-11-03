namespace TAMMi.Core.Agents;

/// <summary>
/// The initial prompt that TAMMi sends an agent when it starts, telling it about the workspace's task context file.
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
    /// The template used until the user changes it. It's a single line, so it can be passed to any agent (see
    /// <c>docs/CONTEXT.md</c>).
    /// </summary>
    public const string DefaultTemplate =
        "The tasks for this workspace are described in " + ContextFilePlaceholder
        + ". Read that file before you start, and read it again when you're told the tasks have changed.";

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
