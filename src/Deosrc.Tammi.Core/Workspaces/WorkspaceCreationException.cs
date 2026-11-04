namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// A workspace couldn't be created. Whatever was created for it has been removed again, except what's listed in
/// <see cref="CleanupProblems"/>.
/// </summary>
public sealed class WorkspaceCreationException : WorkspaceException
{
    public WorkspaceCreationException(
        string message,
        string folder,
        IReadOnlyList<string>? cleanupProblems = null,
        Exception? innerException = null,
        IReadOnlyList<string>? problems = null)
        : base(message, folder, innerException)
    {
        CleanupProblems = cleanupProblems ?? [];
        Problems = problems ?? [];
    }

    /// <summary>
    /// The checks the request failed (see <see cref="WorkspaceCreator.CheckAsync"/>), when it failed them, so nothing
    /// was created. Empty when a step failed instead.
    /// </summary>
    public IReadOnlyList<string> Problems { get; }

    /// <summary>
    /// What couldn't be removed again, so the user can tidy it up. Empty when everything was removed, or when the
    /// workspace failed its checks before anything was created.
    /// </summary>
    public IReadOnlyList<string> CleanupProblems { get; }
}
