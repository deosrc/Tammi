namespace Deosrc.Tammi.Core.Integrations;

/// <summary>
/// Where a Pull Request is in its life.
/// </summary>
public enum PullRequestState
{
    /// <summary>
    /// Open, but marked as a draft: not ready for review yet.
    /// </summary>
    Draft,

    /// <summary>
    /// Open, and ready for review.
    /// </summary>
    Open,

    /// <summary>
    /// Merged into its base branch.
    /// </summary>
    Merged,

    /// <summary>
    /// Closed without being merged (declined).
    /// </summary>
    Closed,
}
