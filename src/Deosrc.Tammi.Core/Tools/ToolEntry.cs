using Deosrc.Tammi.Core.Platform;
using Deosrc.Tammi.Core.Workspaces;

namespace Deosrc.Tammi.Core.Tools;

/// <summary>
/// One item in the Workspace View's tools: a tool, and the folder or solution it opens.
/// </summary>
/// <param name="Label">What it's called, e.g. "VS Code" or "Visual Studio: Api.sln".</param>
/// <param name="Icon">The tool's icon.</param>
/// <param name="Kind">How it opens <paramref name="Path"/>.</param>
/// <param name="Path">The full path of the folder or solution it opens.</param>
/// <param name="Repository">The repository it opens, or <c>null</c> for the Workspace Folder.</param>
/// <param name="Request">What's started, for <see cref="ToolEntryKind.Process"/>; otherwise <c>null</c>.</param>
public sealed record ToolEntry(
    string Label,
    ToolIcon Icon,
    ToolEntryKind Kind,
    string Path,
    WorkspaceRepository? Repository,
    ProcessRequest? Request);
