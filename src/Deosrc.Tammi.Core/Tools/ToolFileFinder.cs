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
    public const int DefaultMaxDepth = 10;

    // Folders that hold build output, packages or tooling rather than source, so their files aren't the repository's
    // own.
    private static readonly HashSet<string> SkippedFolders =
        new(["bin", "obj", "node_modules", "packages"], StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Finds the files in <paramref name="folder"/> that match any of <paramref name="patterns"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The folder and its subfolders are searched, up to <paramref name="maxDepth"/> deep. Hidden folders (whose names
    /// start with <c>.</c>, e.g. <c>.git</c>), build output and package folders, and links to other folders aren't
    /// searched.
    /// </para>
    /// <para>
    /// A pattern with no <c>/</c> (e.g. <c>*.sln</c>) matches the names of the files in each searched folder. A
    /// pattern with a <c>/</c> (e.g. <c>.devcontainer/devcontainer.json</c>) is a path from each searched folder, and
    /// can name hidden folders. A leading <c>/</c> keeps a pattern to <paramref name="folder"/> itself, so
    /// <c>/global.json</c> only matches there.
    /// </para>
    /// <para><c>*</c> and <c>?</c> are wildcards. Case is ignored. Folders that can't be read are skipped.</para>
    /// </remarks>
    /// <returns>
    /// The files found, each once, ordered by their path within <paramref name="folder"/>. A file that several patterns
    /// match has the match folder of the first of them. Empty if the folder doesn't exist.
    /// </returns>
    public static IReadOnlyList<ToolFileMatch> Find(
        string folder,
        IEnumerable<string> patterns,
        int maxDepth = DefaultMaxDepth)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        ArgumentNullException.ThrowIfNull(patterns);
        ArgumentOutOfRangeException.ThrowIfNegative(maxDepth);

        var root = Path.GetFullPath(folder);
        var parsed = patterns.Select(Parse).OfType<Pattern>().ToArray();

        // Each file's match, with the index of the pattern that found it, so the first pattern's is kept.
        var matches = new Dictionary<string, (int Pattern, string Folder)>(StringComparer.Ordinal);
        if (parsed.Length > 0)
        {
            Search(new DirectoryInfo(root), parsed, maxDepth, isRoot: true, matches);
        }

        return matches
            .OrderBy(
                match => Path.GetRelativePath(root, match.Key).Replace('\\', '/'),
                StringComparer.OrdinalIgnoreCase)
            .ThenBy(match => match.Key, StringComparer.Ordinal)
            .Select(match => new ToolFileMatch(match.Key, match.Value.Folder))
            .ToArray();
    }

    // A pattern's path segments, e.g. [".devcontainer", "devcontainer.json"], and whether it only matches at the root.
    private sealed record Pattern(int Index, string[] Segments, bool Anchored);

    private static Pattern? Parse(string? pattern, int index)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return null;
        }

        var normalized = pattern.Trim().Replace('\\', '/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length == 0 || segments.Contains("..") || segments.Contains(".")
            ? null
            : new Pattern(index, segments, normalized.StartsWith('/'));
    }

    private static void Search(
        DirectoryInfo directory,
        Pattern[] patterns,
        int depth,
        bool isRoot,
        Dictionary<string, (int Pattern, string Folder)> matches)
    {
        var entries = Entries(directory);
        foreach (var pattern in patterns.Where(pattern => isRoot || !pattern.Anchored))
        {
            var found = new List<string>();
            FindPath(entries, pattern.Segments, found);
            foreach (var file in found)
            {
                // The pattern matched from this folder: it's the file's folder for a file name, or where a path starts.
                if (!matches.TryGetValue(file, out var existing) || pattern.Index < existing.Pattern)
                {
                    matches[file] = (pattern.Index, directory.FullName);
                }
            }
        }

        if (depth == 0)
        {
            return;
        }

        foreach (var subfolder in entries.OfType<DirectoryInfo>().Where(ShouldSearch))
        {
            Search(subfolder, patterns, depth - 1, isRoot: false, matches);
        }
    }

    // Finds the files at a path of segments, each a name or wildcard, from a folder's entries.
    private static void FindPath(FileSystemInfo[] entries, ReadOnlySpan<string> segments, List<string> files)
    {
        var last = segments.Length == 1;
        var segment = segments[0];
        foreach (var entry in entries)
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
                FindPath(Entries(subfolder), segments[1..], files);
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
