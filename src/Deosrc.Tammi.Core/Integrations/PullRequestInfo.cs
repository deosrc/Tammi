namespace Deosrc.Tammi.Core.Integrations;

/// <summary>
/// A Pull Request, as an integration reads it.
/// </summary>
/// <param name="Source">The <see cref="IIntegration.Id"/> of the integration it was read from.</param>
/// <param name="Repository">The repository it's on (its base repository), e.g. <c>acme/api</c>.</param>
/// <param name="Number">Its number in <paramref name="Repository"/>.</param>
/// <param name="Title">Its title.</param>
/// <param name="Url">Its web page.</param>
/// <param name="State">Whether it's a draft, open, merged or closed.</param>
public sealed record PullRequestInfo(
    string Source,
    string Repository,
    int Number,
    string Title,
    Uri Url,
    PullRequestState State)
{
    /// <summary>
    /// The combined result of its checks, or <see langword="null"/> when it has none or they can't be read.
    /// </summary>
    public PullRequestChecks? Checks { get; init; }

    /// <summary>
    /// What its reviewers decided, or <see langword="null"/> when it needs no review or the service doesn't say.
    /// </summary>
    public PullRequestReview? Review { get; init; }
}
