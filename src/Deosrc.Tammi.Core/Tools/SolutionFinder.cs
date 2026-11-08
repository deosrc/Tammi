namespace Deosrc.Tammi.Core.Tools;

/// <summary>
/// Finds the solution files (<c>.sln</c> and <c>.slnx</c>) in a folder, for tools that open solutions rather than
/// folders (see <see cref="BuiltInToolKind.Solution"/>).
/// </summary>
public static class SolutionFinder
{
    /// <summary>
    /// How many folders deep solutions are looked for: the folder itself, and up to this many levels of subfolders
    /// (e.g. <c>src/Api/Api.sln</c>).
    /// </summary>
    public const int DefaultMaxDepth = 3;

    private static readonly string[] Extensions = [".sln", ".slnx"];

    // Folders that hold build output, packages or tooling rather than source, so their solutions aren't the
    // repository's own.
    private static readonly HashSet<string> SkippedFolders =
        new(["bin", "obj", "node_modules", "packages"], StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Finds the solution files in <paramref name="folder"/> and its subfolders.
    /// </summary>
    /// <remarks>
    /// Hidden folders (whose names start with <c>.</c>, e.g. <c>.git</c>), build output and package folders, and links
    /// to other folders aren't searched. Folders that can't be read are skipped.
    /// </remarks>
    /// <returns>
    /// The solutions' full paths, ordered by their path within <paramref name="folder"/>. Empty if the folder doesn't
    /// exist.
    /// </returns>
    public static IReadOnlyList<string> Find(string folder, int maxDepth = DefaultMaxDepth)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        ArgumentOutOfRangeException.ThrowIfNegative(maxDepth);

        var root = Path.GetFullPath(folder);
        var solutions = new List<string>();
        Search(new DirectoryInfo(root), maxDepth, solutions);
        return solutions
            .OrderBy(path => Path.GetRelativePath(root, path).Replace('\\', '/'), StringComparer.OrdinalIgnoreCase)
            .ThenBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private static void Search(DirectoryInfo directory, int depth, List<string> solutions)
    {
        if (!directory.Exists)
        {
            return;
        }

        FileSystemInfo[] entries;
        try
        {
            entries = directory.GetFileSystemInfos();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (var entry in entries)
        {
            if (entry is FileInfo file && Extensions.Contains(file.Extension, StringComparer.OrdinalIgnoreCase))
            {
                solutions.Add(file.FullName);
            }
            else if (entry is DirectoryInfo subfolder && depth > 0 && ShouldSearch(subfolder))
            {
                Search(subfolder, depth - 1, solutions);
            }
        }
    }

    private static bool ShouldSearch(DirectoryInfo folder) =>
        !folder.Name.StartsWith('.')
        && !SkippedFolders.Contains(folder.Name)
        && folder.LinkTarget is null
        && !folder.Attributes.HasFlag(FileAttributes.ReparsePoint);
}
