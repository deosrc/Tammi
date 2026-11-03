using TAMMi.Core.Agents;

namespace TAMMi.Core.Settings;

/// <summary>
/// The Agents section of the Settings screen.
/// </summary>
public sealed record AgentSettings
{
    /// <summary>
    /// The template for the initial prompt sent to agents when they start, with
    /// <see cref="InitialPrompt.ContextFilePlaceholder"/> for the task context file's path.
    /// </summary>
    public string InitialPromptTemplate { get; init; } = InitialPrompt.DefaultTemplate;

    /// <summary>
    /// Replaces a missing template with the default.
    /// </summary>
    public AgentSettings Normalize() => this with
    {
        InitialPromptTemplate = string.IsNullOrWhiteSpace(InitialPromptTemplate)
            ? InitialPrompt.DefaultTemplate
            : InitialPromptTemplate,
    };
}
