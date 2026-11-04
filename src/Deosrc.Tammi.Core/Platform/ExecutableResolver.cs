namespace TAMMi.Core.Platform;

/// <summary>
/// Finds executables on a search path, the way a shell does.
/// </summary>
/// <param name="searchPath">The search path, in the format of the <c>PATH</c> environment variable.</param>
/// <param name="extensions">
/// The extensions to try after a name, in order (e.g. <c>PATHEXT</c> on Windows). Include an empty string to try the
/// name as it is.
/// </param>
/// <param name="isExecutable">Checks whether a full path is an executable file.</param>
public sealed class ExecutableResolver(
    string? searchPath,
    IReadOnlyList<string> extensions,
    Func<string, bool> isExecutable)
{
    /// <summary>
    /// Finds an executable by name or path.
    /// </summary>
    /// <param name="name">
    /// An executable name, searched for on the search path, or a path (anything with a folder in it), which is only
    /// checked.
    /// </param>
    /// <returns>The executable's full path, or <c>null</c> if it can't be found.</returns>
    public string? Resolve(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (Path.IsPathRooted(name) || name.IndexOfAny(['/', Path.DirectorySeparatorChar]) >= 0)
        {
            return FindInDirectory(Path.GetDirectoryName(Path.GetFullPath(name))!, Path.GetFileName(name));
        }

        foreach (var directory in SearchDirectories())
        {
            if (FindInDirectory(directory, name) is { } path)
            {
                return path;
            }
        }

        return null;
    }

    private IEnumerable<string> SearchDirectories() =>
        (searchPath ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(directory => directory.Trim('"'))
            .Where(Path.IsPathRooted);

    private string? FindInDirectory(string directory, string fileName)
    {
        foreach (var candidate in CandidateFileNames(fileName))
        {
            var path = Path.Combine(directory, candidate);
            if (isExecutable(path))
            {
                return path;
            }
        }

        return null;
    }

    private IEnumerable<string> CandidateFileNames(string fileName)
    {
        // A name that already ends with a known extension (e.g. "code.cmd") is tried as it is first.
        var extension = Path.GetExtension(fileName);
        if (extension.Length > 0 && extensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            yield return fileName;
        }

        foreach (var candidateExtension in extensions)
        {
            yield return fileName + candidateExtension;
        }
    }
}
