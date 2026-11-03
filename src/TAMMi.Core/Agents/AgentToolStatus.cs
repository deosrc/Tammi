namespace TAMMi.Core.Agents;

/// <summary>
/// Whether a Supported Agent's CLI is installed and runs, as found by <see cref="SupportedAgentChecker"/>.
/// </summary>
/// <param name="ExecutablePath">The CLI's full path, or <c>null</c> if it can't be found.</param>
/// <param name="Version">What the CLI reported as its version, or <c>null</c> if it couldn't be run.</param>
/// <param name="Problem">Why the agent can't be used, or <c>null</c> if it can.</param>
public sealed record AgentToolStatus(string? ExecutablePath, string? Version, string? Problem)
{
    /// <summary>
    /// Whether the CLI was found and ran.
    /// </summary>
    public bool IsAvailable => Problem is null;
}
