namespace TAMMi.UI.Workspaces;

/// <summary>
/// The warning each host shows when the user closes TAMMi while agents are running (see <c>docs/CONTEXT.md</c>).
/// </summary>
public static class AppCloseWarning
{
    /// <summary>
    /// The warning's title.
    /// </summary>
    public const string Title = "Close TAMMi?";

    /// <summary>
    /// The warning's text, for <paramref name="runningAgents"/> running agents.
    /// </summary>
    public static string Message(int runningAgents) =>
        (runningAgents == 1 ? "An agent is running." : $"{runningAgents} agents are running.")
        + " Closing TAMMi stops every agent and terminal. Sessions that can be resumed can be resumed when it opens"
        + " again.\n\nClose TAMMi anyway?";
}
