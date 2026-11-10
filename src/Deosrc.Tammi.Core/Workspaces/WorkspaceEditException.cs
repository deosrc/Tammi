namespace Deosrc.Tammi.Core.Workspaces;

/// <summary>
/// A workspace couldn't be edited. Whatever was added for the edit has been removed again, except what's listed in
/// <see cref="CleanupProblems"/>, and the workspace's metadata is as it was.
/// </summary>
public sealed class WorkspaceEditException : WorkspaceException
{
    public WorkspaceEditException(
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
    /// The checks the request failed (see <see cref="WorkspaceEditor.CheckAsync"/>), when it failed them, so nothing
    /// was changed. Empty when a step failed instead.
    /// </summary>
    public IReadOnlyList<string> Problems { get; }

    /// <summary>
    /// What couldn't be removed again, so the user can tidy it up.
    /// </summary>
    public IReadOnlyList<string> CleanupProblems { get; }
}
