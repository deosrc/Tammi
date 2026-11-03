using TAMMi.Core.Agents;

namespace TAMMi.Core.Settings;

/// <summary>
/// The Agents section of the Settings screen.
/// </summary>
public sealed record AgentSettings
{
    /// <summary>
    /// The Agent Definitions that can be started, in the order they're offered. Until the user changes them, these are
    /// the built-in definitions (see <see cref="AgentDefinition.BuiltIn"/>). The user can remove every definition.
    /// </summary>
    public IReadOnlyList<AgentDefinition> Definitions { get; init; } = AgentDefinition.BuiltIn;

    /// <summary>
    /// The template for the initial prompt sent to agents when they start, with
    /// <see cref="InitialPrompt.ContextFilePlaceholder"/> for the task context file's path.
    /// </summary>
    public string InitialPromptTemplate { get; init; } = InitialPrompt.DefaultTemplate;

    /// <summary>
    /// Replaces missing definitions with the built-in ones and a missing template with the default, and fills in
    /// missing values in each definition.
    /// </summary>
    public AgentSettings Normalize() => this with
    {
        Definitions = Definitions is null
            ? AgentDefinition.BuiltIn
            : Definitions.Where(definition => definition is not null).Select(definition => definition.Normalize())
                .ToArray(),
        InitialPromptTemplate = string.IsNullOrWhiteSpace(InitialPromptTemplate)
            ? InitialPrompt.DefaultTemplate
            : InitialPromptTemplate,
    };

    /// <inheritdoc />
    public bool Equals(AgentSettings? other) =>
        other is not null
        && InitialPromptTemplate == other.InitialPromptTemplate
        && (Definitions is null || other.Definitions is null
            ? Definitions == other.Definitions
            : Definitions.SequenceEqual(other.Definitions));

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(InitialPromptTemplate, Definitions?.Count);
}
