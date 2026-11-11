namespace Deosrc.Tammi.Core.Integrations;

/// <summary>
/// What the reviewers of a Pull Request have decided, combined.
/// </summary>
public enum PullRequestReview
{
    /// <summary>
    /// Reviewers are required, and none has approved or asked for changes yet.
    /// </summary>
    ReviewRequired,

    /// <summary>
    /// The required reviewers approved it.
    /// </summary>
    Approved,

    /// <summary>
    /// A reviewer asked for changes (which is as close as GitHub comes to rejecting it).
    /// </summary>
    ChangesRequested,
}
