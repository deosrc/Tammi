namespace Deosrc.Tammi.Core.Agents;

/// <summary>
/// A Supported Agent: an agent CLI that Tammi knows how to start. It's always offered, and isn't configured by the
/// user. Other agents can be added as Custom Agents (see <see cref="Settings.AgentSettings.CustomDefinitions"/>).
/// </summary>
/// <param name="Definition">How to start the agent.</param>
/// <param name="VersionArguments">
/// Arguments that make the CLI print its version and exit, used to check that it's installed and runs.
/// </param>
/// <param name="InstallUrl">The page that says how to install the CLI.</param>
public sealed record SupportedAgent(AgentDefinition Definition, IReadOnlyList<string> VersionArguments, string InstallUrl)
{
    /// <summary>
    /// Claude Code.
    /// </summary>
    public static SupportedAgent ClaudeCode { get; } =
        new(AgentDefinition.ClaudeCode, ["--version"], "https://code.claude.com/docs/en/setup");

    /// <summary>
    /// opencode.
    /// </summary>
    public static SupportedAgent OpenCode { get; } =
        new(AgentDefinition.OpenCode, ["--version"], "https://opencode.ai/docs/#install");

    /// <summary>
    /// The Supported Agents, in the order they're offered.
    /// </summary>
    public static IReadOnlyList<SupportedAgent> All { get; } = [ClaudeCode, OpenCode];

    /// <summary>
    /// The agent's name, e.g. "Claude Code".
    /// </summary>
    public string Name => Definition.Name;
}
