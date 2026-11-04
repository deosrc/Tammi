namespace Deosrc.Tammi.Core.Agents;

/// <summary>
/// Keeps the Agent Sessions that can be resumed, so they can be returned to after Tammi restarts.
/// </summary>
/// <remarks>
/// Workspaces keep their sessions in their <c>.tammi/</c> folder (see <c>docs/CONTEXT.md</c>).
/// </remarks>
public interface IAgentSessionStore
{
    /// <summary>
    /// Why the recorded sessions couldn't be read, or <c>null</c> if they could (or haven't been read yet). When they
    /// can't be read, none are listed.
    /// </summary>
    string? Problem { get; }

    /// <summary>
    /// Lists the recorded sessions, most recently started first.
    /// </summary>
    Task<IReadOnlyList<AgentSession>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a session, in place of any recorded with the same ID (e.g. when it's resumed).
    /// </summary>
    /// <exception cref="IOException">The sessions couldn't be saved.</exception>
    /// <exception cref="UnauthorizedAccessException">The sessions couldn't be saved.</exception>
    Task RecordAsync(AgentSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Forgets a session, so it's no longer offered to resume. Forgetting one that isn't recorded does nothing.
    /// </summary>
    /// <exception cref="IOException">The sessions couldn't be saved.</exception>
    /// <exception cref="UnauthorizedAccessException">The sessions couldn't be saved.</exception>
    Task ForgetAsync(string id, CancellationToken cancellationToken = default);
}
