using System.Text.Json.Serialization;
using Deosrc.Tammi.Core.Agents;

namespace Deosrc.Tammi.Core.Settings;

/// <summary>
/// The Agents section of the Settings screen.
/// </summary>
public sealed record AgentSettings
{
    /// <summary>
    /// The Custom Agents: Agent Definitions the user has added, in the order they're offered after the Supported
    /// Agents.
    /// </summary>
    public IReadOnlyList<AgentDefinition> CustomDefinitions { get; init; } = [];

    /// <summary>
    /// The template for the initial prompt sent to agents when they start, with
    /// <see cref="InitialPrompt.ContextFilePlaceholder"/> for the workspace context file's path.
    /// </summary>
    public string InitialPromptTemplate { get; init; } = InitialPrompt.DefaultTemplate;

    /// <summary>
    /// How the initial prompt is given to an agent: typed into its input for the user to send
    /// (<see cref="InitialPromptStyle.Prefill"/>), or sent straight away (<see cref="InitialPromptStyle.Submit"/>).
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<InitialPromptStyle>))]
    public InitialPromptStyle InitialPromptStyle { get; init; } = InitialPromptStyle.Prefill;

    /// <summary>
    /// The name of the agent whose usage the Usage tab shows, or <c>null</c> to use the first one that can be read.
    /// </summary>
    public string? UsageAgentName { get; init; }

    /// <summary>
    /// Every Agent Definition that can be started, in the order they're offered: the Supported Agents (see
    /// <see cref="SupportedAgent.All"/>), then the Custom Agents.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<AgentDefinition> Definitions =>
        [.. SupportedAgent.All.Select(agent => agent.Definition), .. CustomDefinitions];

    /// <summary>
    /// Fills in missing values in each Custom Agent, and replaces a missing template with the default.
    /// </summary>
    public AgentSettings Normalize() => this with
    {
        CustomDefinitions = CustomDefinitions?.Where(definition => definition is not null)
            .Select(definition => definition.Normalize())
            .ToArray() ?? [],
        InitialPromptTemplate = string.IsNullOrWhiteSpace(InitialPromptTemplate)
            ? InitialPrompt.DefaultTemplate
            : InitialPromptTemplate,
        InitialPromptStyle = Enum.IsDefined(InitialPromptStyle) ? InitialPromptStyle : InitialPromptStyle.Prefill,
    };

    /// <summary>
    /// Whether the Custom Agent at <paramref name="index"/> in <see cref="CustomDefinitions"/> has the same name as a
    /// Supported Agent or another Custom Agent, so the user can't tell them apart. Names match when they're the same
    /// apart from case and surrounding spaces. Blank names don't match anything.
    /// </summary>
    public bool HasDuplicateName(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, CustomDefinitions.Count);

        var name = CustomDefinitions[index].Name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        return SupportedAgent.All.Select(agent => agent.Definition)
            .Concat(CustomDefinitions.Where((_, i) => i != index))
            .Any(other => string.Equals(other.Name?.Trim(), name, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public bool Equals(AgentSettings? other) =>
        other is not null
        && InitialPromptTemplate == other.InitialPromptTemplate
        && InitialPromptStyle == other.InitialPromptStyle
        && UsageAgentName == other.UsageAgentName
        && (CustomDefinitions is null || other.CustomDefinitions is null
            ? CustomDefinitions == other.CustomDefinitions
            : CustomDefinitions.SequenceEqual(other.CustomDefinitions));

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(InitialPromptTemplate, InitialPromptStyle, CustomDefinitions?.Count, UsageAgentName);
}
