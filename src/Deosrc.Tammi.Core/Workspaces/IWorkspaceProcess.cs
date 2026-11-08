using Deosrc.Tammi.Core.Terminals;

namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// A process running in a workspace's embedded terminal, shown as a tab in its Workspace View: a terminal or an agent.
/// Its <see cref="IMainAreaTab.Name"/> is unique among the workspace's open processes of its kind.
/// </summary>
public interface IWorkspaceProcess : IMainAreaTab
{
    /// <summary>
    /// The folder the process was started in.
    /// </summary>
    string WorkingDirectory { get; }

    /// <summary>
    /// The process's terminal session, with its output.
    /// </summary>
    TerminalSession Session { get; }

    /// <summary>
    /// Whether the process has ended.
    /// </summary>
    bool HasExited { get; }
}
