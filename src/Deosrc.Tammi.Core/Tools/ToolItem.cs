namespace Deosrc.Tammi.Core.Tools;

/// <summary>
/// A tool as the Workspace View's Tools section lists it: once, with an entry for each level it can be launched at.
/// </summary>
/// <param name="Tool">The tool.</param>
/// <param name="Label">What it's called: its Workspace Folder entry's label, or else its first repository entry's.</param>
/// <param name="FilePath">The file it opens, for a file tool; otherwise <c>null</c>.</param>
/// <param name="WorkspaceEntry">
/// Its entry for the Workspace Folder, or <c>null</c> if it can't be launched there (e.g. its scope is repositories
/// only).
/// </param>
/// <param name="RepositoryEntries">Its entries for the repositories, in the workspace's repository order.</param>
public sealed record ToolItem(
    ToolDefinition Tool,
    string Label,
    string? FilePath,
    ToolEntry? WorkspaceEntry,
    IReadOnlyList<ToolEntry> RepositoryEntries)
{
    /// <summary>
    /// Every entry, in the order they're offered: the Workspace Folder's first, then the repositories'.
    /// </summary>
    public IReadOnlyList<ToolEntry> Entries =>
        WorkspaceEntry is null ? RepositoryEntries : [WorkspaceEntry, .. RepositoryEntries];

    /// <summary>
    /// The entries that start something different, in the order of <see cref="Entries"/>. Entries that start the same
    /// command (the same executable, arguments and working directory) are one launch, e.g. a file tool whose arguments
    /// only depend on the file: only the first of them is kept.
    /// </summary>
    public IReadOnlyList<ToolEntry> Launches =>
        Entries.DistinctBy(entry => entry.Request is { } request
                ? $"{request.FileName}\0{request.WorkingDirectory}\0{string.Join('\0', request.Arguments)}"
                : $"terminal\0{entry.Path}")
            .ToArray();

    /// <summary>
    /// The entry that launching the item opens without asking where: its Workspace Folder entry, or else its only
    /// launch. <c>null</c> when it has several launches and none for the Workspace Folder, so the user picks one.
    /// </summary>
    public ToolEntry? DefaultEntry => WorkspaceEntry ?? (Launches is [var only] ? only : null);
}
