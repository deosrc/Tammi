namespace Deosrc.Tammi.Core.Tools;

/// <summary>
/// A tool as the Workspace View's Tools section lists it: once, whatever its kind, with the places it can be launched.
/// </summary>
/// <param name="Tool">The tool.</param>
/// <param name="Label">What it's called: the tool's name.</param>
/// <param name="WorkspaceEntry">
/// Its entry for the Workspace Folder, or <c>null</c> if it can't be launched there (e.g. its scope is repositories
/// only, or it's a file tool, whose entries are for its files).
/// </param>
/// <param name="RepositoryEntries">Its entries for the repositories, in the workspace's repository order.</param>
public sealed record ToolItem(
    ToolDefinition Tool,
    string Label,
    ToolEntry? WorkspaceEntry,
    IReadOnlyList<ToolEntry> RepositoryEntries)
{
    /// <summary>
    /// Every entry, in the order they're offered: the Workspace Folder's first, then the repositories'.
    /// </summary>
    public IReadOnlyList<ToolEntry> Entries =>
        WorkspaceEntry is null ? RepositoryEntries : [WorkspaceEntry, .. RepositoryEntries];

    /// <summary>
    /// The places the tool can be launched, as its menu lists them. A folder tool's are its Workspace Folder, then each
    /// repository. A file tool's are the files it found, each once, wherever its entry is from.
    /// </summary>
    public IReadOnlyList<ToolEntry> Places => Tool.Kind == ToolKind.File
        ? Entries.DistinctBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase).ToArray()
        : Entries;

    /// <summary>
    /// The entry that launching the item opens without asking where, or <c>null</c> when the user picks from the menu.
    /// That's its only place, or, for a folder tool in a workspace with one repository, the one the single repository
    /// setting prefers: the repository or the Workspace Folder. With several places, there's none.
    /// </summary>
    public ToolEntry? DefaultEntry { get; init; }
}
