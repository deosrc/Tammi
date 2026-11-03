namespace TAMMi.Core.Agents;

/// <summary>
/// An Agent Definition: a configured agent CLI that TAMMi can start.
/// </summary>
/// <param name="Name">The name shown to the user, e.g. "Claude Code".</param>
/// <param name="Command">
/// The executable's name or path. Names are found on the search path, as a shell would find them.
/// </param>
/// <param name="Arguments">The arguments, each passed to the agent as-is.</param>
public sealed record AgentDefinition(string Name, string Command, IReadOnlyList<string> Arguments)
{
    /// <summary>
    /// Creates a definition from a command and its arguments.
    /// </summary>
    public AgentDefinition(string name, string command, params string[] arguments)
        : this(name, command, (IReadOnlyList<string>)arguments)
    {
    }

    /// <summary>
    /// The built-in definition for Claude Code.
    /// </summary>
    public static AgentDefinition ClaudeCode { get; } = new("Claude Code", "claude");

    /// <summary>
    /// The built-in definitions, in the order they're offered.
    /// </summary>
    public static IReadOnlyList<AgentDefinition> BuiltIn { get; } = [ClaudeCode];
}
