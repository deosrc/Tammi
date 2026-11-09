namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// Arranges a commit's changed files by folder, for the git graph's details.
/// </summary>
public static class CommitFileTree
{
    private static readonly StringComparer NameComparer = StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// The folders and files at the repository root, folders first, each in name order. A folder that holds only one
    /// folder, and no files, is joined with it (e.g. <c>src/app</c>), so the tree isn't deeper than it needs to be.
    /// </summary>
    public static IReadOnlyList<CommitFileTreeNode> Build(IEnumerable<CommitFileChange> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);

        var root = new Folder();
        foreach (CommitFileChange change in changes)
        {
            string[] parts = change.Path.Split('/');
            Folder folder = root;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (!folder.Folders.TryGetValue(parts[i], out Folder? child))
                {
                    child = new Folder();
                    folder.Folders.Add(parts[i], child);
                }

                folder = child;
            }

            folder.Files.Add((parts[^1], change));
        }

        return Nodes(root, "");
    }

    /// <summary>
    /// Every file in a folder of the tree, however deep.
    /// </summary>
    public static IEnumerable<CommitFileChange> FilesIn(CommitFileTreeNode folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        return folder.Children.SelectMany(child => child.Change is { } change ? [change] : FilesIn(child));
    }

    /// <summary>
    /// A file's folder, with <c>/</c> separators, or an empty string at the repository root.
    /// </summary>
    public static string FolderOf(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        int slash = path.LastIndexOf('/');
        return slash < 0 ? "" : path[..slash];
    }

    /// <summary>
    /// A file's name, without its folder.
    /// </summary>
    public static string NameOf(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return path[(path.LastIndexOf('/') + 1)..];
    }

    private static CommitFileTreeNode[] Nodes(Folder folder, string path)
    {
        var folders = folder.Folders
            .OrderBy(entry => entry.Key, NameComparer)
            .Select(entry => FolderNode(entry.Key, Join(path, entry.Key), entry.Value));
        var files = folder.Files
            .OrderBy(file => file.Name, NameComparer)
            .Select(file => new CommitFileTreeNode(file.Name, file.Change.Path, [], file.Change));
        return [.. folders, .. files];
    }

    private static CommitFileTreeNode FolderNode(string name, string path, Folder folder)
    {
        // Join a folder holding just one folder with it.
        while (folder.Files.Count == 0 && folder.Folders.Count == 1)
        {
            (string childName, Folder child) = folder.Folders.Single();
            name = $"{name}/{childName}";
            path = Join(path, childName);
            folder = child;
        }

        return new CommitFileTreeNode(name, path, Nodes(folder, path), null);
    }

    private static string Join(string folder, string name) => folder.Length == 0 ? name : $"{folder}/{name}";

    private sealed class Folder
    {
        public Dictionary<string, Folder> Folders { get; } = new(StringComparer.Ordinal);

        public List<(string Name, CommitFileChange Change)> Files { get; } = [];
    }
}
