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
    /// The placeholder in <see cref="PromptArguments"/> for the initial prompt.
    /// </summary>
    public const string PromptPlaceholder = "{prompt}";

    /// <summary>
    /// Creates a definition from a command and its arguments.
    /// </summary>
    public AgentDefinition(string name, string command, params string[] arguments)
        : this(name, command, (IReadOnlyList<string>)arguments)
    {
    }

    /// <summary>
    /// How the initial prompt is passed to the CLI: arguments added after <see cref="Arguments"/>, with
    /// <see cref="PromptPlaceholder"/> where the prompt goes. For example, <c>["{prompt}"]</c> passes it as a
    /// positional argument and <c>["--prompt", "{prompt}"]</c> as an option's value. Empty when the agent doesn't
    /// take an initial prompt.
    /// </summary>
    public IReadOnlyList<string> PromptArguments { get; init; } = [];

    /// <summary>
    /// Whether the agent takes an initial prompt.
    /// </summary>
    public bool AcceptsInitialPrompt => PromptArguments.Count > 0;

    /// <summary>
    /// The built-in definition for Claude Code. The initial prompt is its positional argument, which starts an
    /// interactive session with that prompt.
    /// </summary>
    public static AgentDefinition ClaudeCode { get; } = new("Claude Code", "claude")
    {
        PromptArguments = [PromptPlaceholder],
    };

    /// <summary>
    /// The built-in definitions, in the order they're offered.
    /// </summary>
    public static IReadOnlyList<AgentDefinition> BuiltIn { get; } = [ClaudeCode];

    /// <summary>
    /// The arguments to start the agent with.
    /// </summary>
    /// <param name="initialPrompt">
    /// The initial prompt, or <c>null</c> for none. It's left out if the agent doesn't take one.
    /// </param>
    /// <returns>
    /// <see cref="Arguments"/>, followed by <see cref="PromptArguments"/> with the prompt in place of the placeholder
    /// when there's a prompt. Each argument is passed to the agent as-is, so the prompt needs no quoting.
    /// </returns>
    public IReadOnlyList<string> BuildArguments(string? initialPrompt)
    {
        if (string.IsNullOrEmpty(initialPrompt) || !AcceptsInitialPrompt)
        {
            return Arguments;
        }

        return
        [
            .. Arguments,
            .. PromptArguments.Select(argument =>
                argument.Replace(PromptPlaceholder, initialPrompt, StringComparison.Ordinal)),
        ];
    }
}
