namespace TAMMi.Core.Git;

/// <summary>
/// The given path isn't inside a git repository.
/// </summary>
public sealed class GitRepositoryNotFoundException(string path, Exception? innerException = null)
    : GitException($"'{path}' isn't a git repository.", innerException)
{
    /// <summary>
    /// The path that was opened.
    /// </summary>
    public string Path { get; } = path;
}
