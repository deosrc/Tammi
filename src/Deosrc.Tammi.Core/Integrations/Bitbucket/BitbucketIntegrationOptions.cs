namespace Deosrc.Tammi.Core.Integrations.Bitbucket;

/// <summary>
/// How a <see cref="BitbucketIntegration"/> connects to Bitbucket Cloud.
/// </summary>
public sealed record BitbucketIntegrationOptions
{
    /// <summary>
    /// The <see cref="IIntegration.Id"/> of the Bitbucket Cloud integration. There's only one Bitbucket Cloud, so the
    /// user can connect it once.
    /// </summary>
    public const string DefaultId = "bitbucket";

    /// <summary>
    /// Bitbucket Cloud's REST API.
    /// </summary>
    public static Uri CloudApiUrl { get; } = new("https://api.bitbucket.org/2.0/");

    /// <summary>
    /// The host Bitbucket Cloud's web pages and git remotes are served from.
    /// </summary>
    public const string WebHost = "bitbucket.org";

    /// <summary>
    /// The integration's <see cref="IIntegration.Id"/>. It also names its secret in the credential store.
    /// </summary>
    public string Id { get; init; } = DefaultId;

    /// <summary>
    /// The integration's <see cref="IIntegration.Name"/>.
    /// </summary>
    public string Name { get; init; } = "Bitbucket";

    /// <summary>
    /// The email address of the user's Atlassian account, which Bitbucket Cloud needs with the API token. It isn't
    /// secret, so it's a setting rather than part of the stored secret.
    /// </summary>
    public required string Email { get; init; }

    /// <summary>
    /// The REST API to call.
    /// </summary>
    public Uri ApiUrl { get; init; } = CloudApiUrl;

    /// <summary>
    /// How many results to ask for in each page, up to Bitbucket's maximum of 100.
    /// </summary>
    public int PageSize { get; init; } = 100;

    /// <summary>
    /// The longest Tammi waits for a rate limit to reset before retrying a request. When Bitbucket asks for a longer
    /// wait, the request fails with an <see cref="IntegrationRateLimitException"/> instead.
    /// </summary>
    public TimeSpan MaxRateLimitWait { get; init; } = TimeSpan.FromSeconds(10);
}
