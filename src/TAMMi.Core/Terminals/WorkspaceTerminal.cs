using TAMMi.Core.Platform;

namespace TAMMi.Core.Terminals;

/// <summary>
/// A terminal in a workspace: the terminal shell, running in the Workspace Folder or one of its repositories.
/// </summary>
/// <remarks>
/// It keeps running whether or not the workspace's tab is open. Close it with <see cref="WorkspaceTerminals"/>.
/// </remarks>
public sealed class WorkspaceTerminal
{
    internal WorkspaceTerminal(string workspaceId, string name, string workingDirectory, IPtySession pty)
    {
        WorkspaceId = workspaceId;
        Name = name;
        WorkingDirectory = workingDirectory;
        Session = new TerminalSession(pty);
    }

    /// <summary>
    /// The ID of the workspace the terminal belongs to.
    /// </summary>
    public string WorkspaceId { get; }

    /// <summary>
    /// The terminal's name, unique among the workspace's open terminals, e.g. "Terminal 2" or "api".
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The folder the shell was started in.
    /// </summary>
    public string WorkingDirectory { get; }

    /// <summary>
    /// The shell's session, with its output.
    /// </summary>
    public TerminalSession Session { get; }

    /// <summary>
    /// Whether the shell has exited.
    /// </summary>
    public bool HasExited => Session.Pty.Exited.IsCompleted;
}
