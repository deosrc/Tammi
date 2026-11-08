using System.IO.Enumeration;

namespace Deosrc.Tammi.Core.Tools;

/// <summary>
/// Finds the files a file tool opens in a repository (see <see cref="ToolDefinition.FilePatterns"/>).
/// </summary>
public static class ToolFileFinder
{
    /// <summary>
    /// How many folders deep file names are looked for: the folder itself, and up to this many levels of subfolders
    /// (e.g. <c>src/Api/Api.sln</c>).
    /// </summary>
    public const int DefaultMaxDepth = 3;

    // Folders that hold build output, packages or tooling rather than source, so their files aren't the repository's
    // own.
    private static readonly HashSet<string> SkippedFolders =
        new(["bin", "obj", "node_modules", "packages"], StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Finds the files in <paramref name="folder"/> that match any of <paramref name="patterns"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A pattern with no <c>/</c> (e.g. <c>*.sln</c>) matches file names in the folder and its subfolders, up to
    /// <paramref name="maxDepth"/> deep. Hidden folders (whose names start with <c>.</c>, e.g. <c>.git</c>), build
    /// output and package folders, and links to other folders aren't searched.
    /// </para>
    /// <para>
    /// A pattern with a <c>/</c> (e.g. <c>.devcontainer/devcontainer.json</c>) is a path from the folder, and can name
    /// hidden folders. A leading <c>/</c> is ignored, so <c>/global.json</c> only matches in the folder itself.
    /// </para>
    /// <para><c>*</c> and <c>?</c> are wildcards. Case is ignored. Folders that can't be read are skipped.</para>
    /// </remarks>
    /// <returns>
    /// The files' full paths, each once, ordered by their path within <paramref name="folder"/>. Empty if the folder
    /// doesn't exist.
    /// </returns>
    public static IReadOnlyList<string> Find(
        string folder,
        IEnumerable<string> patterns,
        int maxDepth = DefaultMaxDepth)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        ArgumentNullException.ThrowIfNull(patterns);
        ArgumentOutOfRangeException.ThrowIfNegative(maxDepth);

        var root = Path.GetFullPath(folder);
        var namePatterns = new List<string>();
        var files = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pattern in patterns.Where(pattern => !string.IsNullOrWhiteSpace(pattern)))
        {
            var normalized = pattern.Trim().Replace('\\', '/');
            if (normalized.Contains('/'))
            {
                var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length > 0 && !segments.Contains("..") && !segments.Contains("."))
                {
                    FindPath(new DirectoryInfo(root), segments, files);
                }
            }
            else
            {
                namePatterns.Add(normalized);
            }
        }

        if (namePatterns.Count > 0)
        {
            FindNames(new DirectoryInfo(root), namePatterns, maxDepth, files);
        }

        return files
            .OrderBy(path => Path.GetRelativePath(root, path).Replace('\\', '/'), StringComparer.OrdinalIgnoreCase)
            .ThenBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private static void FindNames(DirectoryInfo directory, List<string> patterns, int depth, HashSet<string> files)
    {
        foreach (var entry in Entries(directory))
        {
            if (entry is FileInfo file && patterns.Any(pattern => Matches(pattern, file.Name)))
            {
                files.Add(file.FullName);
            }
            else if (entry is DirectoryInfo subfolder && depth > 0 && ShouldSearch(subfolder))
            {
                FindNames(subfolder, patterns, depth - 1, files);
            }
        }
    }

    private static void FindPath(DirectoryInfo directory, ReadOnlySpan<string> segments, HashSet<string> files)
    {
        var last = segments.Length == 1;
        var segment = segments[0];
        foreach (var entry in Entries(directory))
        {
            if (!Matches(segment, entry.Name))
            {
                continue;
            }

            if (last && entry is FileInfo file)
            {
                files.Add(file.FullName);
            }
            else if (!last && entry is DirectoryInfo subfolder && IsNotLink(subfolder))
            {
                FindPath(subfolder, segments[1..], files);
            }
        }
    }

    private static FileSystemInfo[] Entries(DirectoryInfo directory)
    {
        try
        {
            return directory.Exists ? directory.GetFileSystemInfos() : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static bool Matches(string pattern, string name) =>
        FileSystemName.MatchesSimpleExpression(pattern, name, ignoreCase: true);

    private static bool ShouldSearch(DirectoryInfo folder) =>
        !folder.Name.StartsWith('.') && !SkippedFolders.Contains(folder.Name) && IsNotLink(folder);

    private static bool IsNotLink(DirectoryInfo folder) =>
        folder.LinkTarget is null && !folder.Attributes.HasFlag(FileAttributes.ReparsePoint);
}
