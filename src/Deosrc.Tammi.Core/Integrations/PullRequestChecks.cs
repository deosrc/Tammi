namespace Deosrc.Tammi.Core.Integrations;

/// <summary>
/// The combined result of a Pull Request's checks (e.g. CI builds and commit statuses) on its latest commit: the most
/// severe of them.
/// </summary>
public enum PullRequestChecks
{
    /// <summary>
    /// Every check passed.
    /// </summary>
    Succeeded,

    /// <summary>
    /// No check failed, but at least one hasn't finished (or hasn't started yet).
    /// </summary>
    InProgress,

    /// <summary>
    /// At least one check failed.
    /// </summary>
    Failed,
}
