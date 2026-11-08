using Deosrc.Tammi.Core.Platform;
using Deosrc.Tammi.Core.Workspaces;

namespace Deosrc.Tammi.Core.Tools;

/// <summary>
/// One item in the Workspace View's tools: a tool, and the folder or file it opens.
/// </summary>
/// <param name="Tool">The tool.</param>
/// <param name="Label">What it's called: the tool's name, with its placeholders filled in.</param>
/// <param name="Path">The full path of the folder or file it opens, shown as its tooltip.</param>
/// <param name="Repository">The repository the folder or file is in, or <c>null</c> for the Workspace Folder.</param>
/// <param name="Request">
/// What's started: the tool's executable, found on the search path, and its arguments with the placeholders filled in.
/// <c>null</c> for the Terminal tool, which the Workspace View opens in a tab.
/// </param>
public sealed record ToolEntry(
    ToolDefinition Tool,
    string Label,
    string Path,
    WorkspaceRepository? Repository,
    ProcessRequest? Request)
{
    /// <summary>
    /// Whether the entry opens one of Tammi's own terminals in the Workspace View (see
    /// <see cref="ToolDefinition.OpensTerminal"/>), in <see cref="Repository"/> or the Workspace Folder.
    /// </summary>
    public bool OpensTerminal => Tool.OpensTerminal;

    /// <summary>
    /// The icon it's shown with, once found (see <see cref="ToolImages"/>), or <c>null</c> until then.
    /// </summary>
    public ToolImage? Image { get; init; }
}
