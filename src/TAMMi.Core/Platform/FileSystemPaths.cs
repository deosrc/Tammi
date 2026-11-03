namespace TAMMi.Core.Platform;

/// <summary>
/// Compares and tidies the paths of files and folders on the local file system.
/// </summary>
public static class FileSystemPaths
{
    /// <summary>
    /// Compares paths as the platform's file system does: ignoring case on Windows, and exactly elsewhere.
    /// </summary>
    public static StringComparer Comparer { get; } =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>
    /// Makes <paramref name="path"/> a full path without a trailing separator (other than a root's), so the same
    /// folder is always written the same way.
    /// </summary>
    public static string Normalize(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path.Trim()));
    }

    /// <summary>
    /// Normalizes each path (see <see cref="Normalize"/>), and drops blank paths and repeats, keeping the order.
    /// </summary>
    public static IReadOnlyList<string> NormalizeAll(IEnumerable<string?>? paths)
    {
        if (paths is null)
        {
            return [];
        }

        var seen = new HashSet<string>(Comparer);
        var result = new List<string>();
        foreach (string? path in paths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            string normalized = Normalize(path);
            if (seen.Add(normalized))
            {
                result.Add(normalized);
            }
        }

        return result;
    }
}
