using System.Text.Json.Serialization;

namespace Deosrc.Tammi.Core.Agents;

/// <summary>
/// An Agent Definition: a configured agent CLI that Tammi can start.
/// </summary>
/// <remarks>
/// Supported Agents have built-in definitions (see <see cref="SupportedAgent"/>). Custom Agents' definitions are kept in
/// the settings file and edited on the Settings screen. Two definitions are equal when their values are, including
/// their arguments.
/// </remarks>
public sealed record AgentDefinition
{
    /// <summary>
    /// The placeholder in <see cref="PromptArguments"/> for the initial prompt.
    /// </summary>
    public const string PromptPlaceholder = "{prompt}";

    /// <summary>
    /// The placeholder in <see cref="SessionIdArguments"/> and <see cref="ResumeArguments"/> for the Agent Session's
    /// ID.
    /// </summary>
    public const string SessionIdPlaceholder = "{sessionId}";

    /// <summary>
    /// Creates a definition.
    /// </summary>
    /// <param name="name">The name shown to the user, e.g. "Claude Code".</param>
    /// <param name="command">
    /// The executable's name or path. Names are found on the search path, as a shell would find them.
    /// </param>
    /// <param name="arguments">The arguments, each passed to the agent as-is.</param>
    [JsonConstructor]
    public AgentDefinition(string name, string command, IReadOnlyList<string> arguments)
    {
        Name = name;
        Command = command;
        Arguments = arguments;
    }

    /// <summary>
    /// Creates a definition from a command and its arguments.
    /// </summary>
    public AgentDefinition(string name, string command, params string[] arguments)
        : this(name, command, (IReadOnlyList<string>)arguments)
    {
    }

    /// <summary>
    /// The name shown to the user, e.g. "Claude Code".
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// The executable's name or path. Names are found on the search path, as a shell would find them.
    /// </summary>
    public string Command { get; init; }

    /// <summary>
    /// The arguments, each passed to the agent as-is.
    /// </summary>
    public IReadOnlyList<string> Arguments { get; init; }

    /// <summary>
    /// How the initial prompt is passed to the CLI: arguments added after <see cref="Arguments"/>, with
    /// <see cref="PromptPlaceholder"/> where the prompt goes. For example, <c>["{prompt}"]</c> passes it as a
    /// positional argument and <c>["--prompt", "{prompt}"]</c> as an option's value. Empty when the agent doesn't
    /// take an initial prompt.
    /// </summary>
    public IReadOnlyList<string> PromptArguments { get; init; } = [];

    /// <summary>
    /// How the agent reports what it's doing through its hooks, or <c>null</c> if it doesn't. Agents without hooks show
    /// <see cref="AgentState.Running"/> while their process runs. Only Supported Agents have hooks for now, so it
    /// isn't kept in the settings file.
    /// </summary>
    [JsonIgnore]
    public AgentHooks? Hooks { get; init; }

    /// <summary>
    /// Whether the agent reports its state in its terminal output (see <c>TerminalStateReader</c>), instead of through
    /// <see cref="Hooks"/>. Only Supported Agents do for now, so it isn't kept in the settings file.
    /// </summary>
    [JsonIgnore]
    public bool ReportsStateInTerminal { get; init; }

    /// <summary>
    /// What the IDs Tammi makes up for new Agent Sessions start with, for agents that only accept IDs in their own
    /// format. <c>null</c> for a UUID. See <see cref="NewSessionId"/>.
    /// </summary>
    [JsonIgnore]
    public string? SessionIdPrefix { get; init; }

    /// <summary>
    /// How Tammi sets a new Agent Session's ID: arguments added after <see cref="Arguments"/> when the agent starts,
    /// with <see cref="SessionIdPlaceholder"/> where the ID goes. Tammi makes up the ID, a UUID, so it can resume the
    /// session later. For example, <c>["--session-id", "{sessionId}"]</c>. Empty when the agent's session ID can't be
    /// set at launch: Tammi then gets it from the agent's hooks, if they report it (see
    /// <see cref="AgentHooks.ReportsSessionId"/>).
    /// </summary>
    public IReadOnlyList<string> SessionIdArguments { get; init; } = [];

    /// <summary>
    /// How an Agent Session is resumed: arguments added after <see cref="Arguments"/> in place of
    /// <see cref="SessionIdArguments"/> and the initial prompt, with <see cref="SessionIdPlaceholder"/> where the
    /// session's ID goes. For example, <c>["--resume", "{sessionId}"]</c>. Empty when the agent's sessions can't be
    /// resumed.
    /// </summary>
    public IReadOnlyList<string> ResumeArguments { get; init; } = [];

    /// <summary>
    /// Whether the agent takes an initial prompt.
    /// </summary>
    [JsonIgnore]
    public bool AcceptsInitialPrompt => PromptArguments.Count > 0;

    /// <summary>
    /// Whether Tammi can resume the agent's sessions: it says how to resume one, and Tammi can get a session's ID,
    /// either by setting it at launch or from the agent's hooks.
    /// </summary>
    [JsonIgnore]
    public bool CanResume =>
        ResumeArguments.Count > 0 && (SessionIdArguments.Count > 0 || Hooks?.ReportsSessionId == true);

    /// <summary>
    /// The definition for Claude Code, a Supported Agent (see <see cref="SupportedAgent.ClaudeCode"/>). The initial
    /// prompt is its positional argument, which starts an interactive session with that prompt. Its state comes from
    /// its hooks (see <see cref="ClaudeCodeHooks"/>). Tammi sets the session's ID with <c>--session-id</c>, follows it
    /// through the hooks, and resumes it with <c>--resume</c>.
    /// </summary>
    public static AgentDefinition ClaudeCode { get; } = new("Claude Code", "claude")
    {
        PromptArguments = [PromptPlaceholder],
        Hooks = ClaudeCodeHooks.Instance,
        SessionIdArguments = ["--session-id", SessionIdPlaceholder],
        ResumeArguments = ["--resume", SessionIdPlaceholder],
    };

    /// <summary>
    /// The definition for opencode, a Supported Agent (see <see cref="SupportedAgent.OpenCode"/>). The initial prompt
    /// is passed with <c>--prompt</c>, which starts the interactive terminal UI with that prompt. Tammi sets the
    /// session's ID with <c>--session</c> (opencode creates the session if it doesn't exist, but only accepts IDs
    /// that start with <c>ses_</c>) and resumes it the same way.
    /// </summary>
    /// <remarks>
    /// Differences from <see cref="ClaudeCode"/>: opencode has no hooks. It reports its state and session in its
    /// terminal output instead (see <see cref="ReportsStateInTerminal"/>), so its state is only known while its output
    /// is being read (see <c>WorkspaceAgent</c>).
    /// </remarks>
    public static AgentDefinition OpenCode { get; } = new("opencode", "opencode")
    {
        PromptArguments = ["--prompt", PromptPlaceholder],
        ReportsStateInTerminal = true,
        SessionIdPrefix = AgentSession.OpenCodeIdPrefix,
        SessionIdArguments = ["--session", SessionIdPlaceholder],
        ResumeArguments = ["--session", SessionIdPlaceholder],
    };

    /// <summary>
    /// Makes up an ID for a new Agent Session, in the format the agent accepts: a UUID, or <see cref="SessionIdPrefix"/>
    /// and 26 hex digits.
    /// </summary>
    public string NewSessionId() => SessionIdPrefix is { } prefix
        ? prefix + Guid.NewGuid().ToString("N")[..26]
        : Guid.NewGuid().ToString();

    /// <summary>
    /// The arguments to start the agent with.
    /// </summary>
    /// <param name="initialPrompt">
    /// The initial prompt, or <c>null</c> for none. It's left out if the agent doesn't take one.
    /// </param>
    /// <param name="sessionId">
    /// The new Agent Session's ID, or <c>null</c> for none. It's left out if the agent's sessions can't be resumed.
    /// </param>
    /// <returns>
    /// <see cref="Arguments"/>, followed by <see cref="SessionIdArguments"/> with the ID in place of the placeholder
    /// when there's an ID and the agent's sessions can be resumed, and then <see cref="PromptArguments"/> with the
    /// prompt in place of the placeholder when there's a prompt. Each argument is passed to the agent as-is, so
    /// neither needs quoting.
    /// </returns>
    public IReadOnlyList<string> BuildArguments(string? initialPrompt, string? sessionId = null)
    {
        IEnumerable<string> arguments = Arguments;
        if (!string.IsNullOrEmpty(sessionId) && CanResume)
        {
            arguments = arguments.Concat(Fill(SessionIdArguments, SessionIdPlaceholder, sessionId));
        }

        if (!string.IsNullOrEmpty(initialPrompt) && AcceptsInitialPrompt)
        {
            arguments = arguments.Concat(Fill(PromptArguments, PromptPlaceholder, initialPrompt));
        }

        return arguments as IReadOnlyList<string> ?? arguments.ToArray();
    }

    /// <summary>
    /// The arguments to resume an Agent Session with.
    /// </summary>
    /// <param name="sessionId">The session's ID.</param>
    /// <returns>
    /// <see cref="Arguments"/>, followed by <see cref="ResumeArguments"/> with the ID in place of the placeholder.
    /// </returns>
    /// <exception cref="InvalidOperationException">The agent's sessions can't be resumed.</exception>
    public IReadOnlyList<string> BuildResumeArguments(string sessionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        if (!CanResume)
        {
            throw new InvalidOperationException($"The agent '{Name}' doesn't say how to resume its sessions.");
        }

        return [.. Arguments, .. Fill(ResumeArguments, SessionIdPlaceholder, sessionId)];
    }

    /// <summary>
    /// Replaces missing values (e.g. from a settings file written by hand) with empty ones, and trims the command.
    /// </summary>
    public AgentDefinition Normalize() => this with
    {
        Name = Name ?? string.Empty,
        Command = Command?.Trim() ?? string.Empty,
        Arguments = Arguments?.Where(argument => argument is not null).ToArray() ?? [],
        PromptArguments = PromptArguments?.Where(argument => argument is not null).ToArray() ?? [],
        SessionIdArguments = SessionIdArguments?.Where(argument => argument is not null).ToArray() ?? [],
        ResumeArguments = ResumeArguments?.Where(argument => argument is not null).ToArray() ?? [],
    };

    /// <inheritdoc />
    public bool Equals(AgentDefinition? other) =>
        other is not null
        && Name == other.Name
        && Command == other.Command
        && Equals(Hooks, other.Hooks)
        && ReportsStateInTerminal == other.ReportsStateInTerminal
        && SessionIdPrefix == other.SessionIdPrefix
        && SequenceEqual(Arguments, other.Arguments)
        && SequenceEqual(PromptArguments, other.PromptArguments)
        && SequenceEqual(SessionIdArguments, other.SessionIdArguments)
        && SequenceEqual(ResumeArguments, other.ResumeArguments);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Name, Command, Arguments?.Count, PromptArguments?.Count);

    private static IEnumerable<string> Fill(IEnumerable<string> arguments, string placeholder, string value) =>
        arguments.Select(argument => argument.Replace(placeholder, value, StringComparison.Ordinal));

    private static bool SequenceEqual(IReadOnlyList<string>? first, IReadOnlyList<string>? second) =>
        first is null || second is null ? first == second : first.SequenceEqual(second);
}
