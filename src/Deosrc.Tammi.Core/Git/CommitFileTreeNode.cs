namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// A folder or file in a <see cref="CommitFileTree"/>.
/// </summary>
/// <param name="Name">
/// The file's name, or the folder's. A folder that holds only one folder is shown as one, so its name can be a path,
/// e.g. <c>src/Deosrc.Tammi.UI</c>.
/// </param>
/// <param name="Path">The full path from the repository root, with <c>/</c> separators.</param>
/// <param name="Children">A folder's folders, then its files, each in name order. A file has none.</param>
/// <param name="Change">A file's change, or <see langword="null"/> for a folder.</param>
public sealed record CommitFileTreeNode(
    string Name,
    string Path,
    IReadOnlyList<CommitFileTreeNode> Children,
    CommitFileChange? Change)
{
    /// <summary>
    /// Whether it's a folder.
    /// </summary>
    public bool IsFolder => Change is null;
}
