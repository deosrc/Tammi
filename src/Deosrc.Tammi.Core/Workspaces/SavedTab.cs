namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// An agent or git graph tab that was open in a workspace, kept so it can be opened again when the workspace is next
/// opened after Tammi restarts. Terminals aren't kept.
/// </summary>
/// <param name="Kind"><see cref="AgentKind"/> or <see cref="GitGraphKind"/>.</param>
/// <param name="SessionId">For an agent, the ID of the Agent Session it was running.</param>
/// <param name="Repository">For a git graph, the folder name of its repository.</param>
/// <param name="SubmodulePath">For a git graph, the submodule's path in the repository, or <c>null</c>.</param>
public sealed record SavedTab(string Kind, string? SessionId = null, string? Repository = null, string? SubmodulePath = null)
{
    /// <summary>
    /// The kind of an agent tab.
    /// </summary>
    public const string AgentKind = "agent";

    /// <summary>
    /// The kind of a git graph tab.
    /// </summary>
    public const string GitGraphKind = "git";
}
