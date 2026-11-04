namespace Deosrc.Tammi.Core.Integrations.GitHub;

/// <summary>
/// How a <see cref="GitHubIntegration"/> connects to GitHub.
/// </summary>
public sealed record GitHubIntegrationOptions
{
    /// <summary>
    /// The <see cref="IIntegration.Id"/> of a github.com integration.
    /// </summary>
    public const string DefaultId = "github";

    /// <summary>
    /// The REST API of github.com.
    /// </summary>
    public static Uri GitHubComApiUrl { get; } = new("https://api.github.com/");

    /// <summary>
    /// The integration's <see cref="IIntegration.Id"/>. It also names its secret in the credential store.
    /// </summary>
    public string Id { get; init; } = DefaultId;

    /// <summary>
    /// The integration's <see cref="IIntegration.Name"/>.
    /// </summary>
    public string Name { get; init; } = "GitHub";

    /// <summary>
    /// The REST API to call: github.com by default, or e.g. <c>https://github.example.com/api/v3/</c> for GitHub
    /// Enterprise Server.
    /// </summary>
    public Uri ApiUrl { get; init; } = GitHubComApiUrl;

    /// <summary>
    /// How many results to ask for in each page, up to GitHub's maximum of 100.
    /// </summary>
    public int PageSize { get; init; } = 100;

    /// <summary>
    /// The most tasks a search returns. GitHub returns at most 1,000 search results.
    /// </summary>
    public int MaxSearchResults { get; init; } = 100;

    /// <summary>
    /// How long the accounts and repositories a text search covers are remembered before they're read again.
    /// </summary>
    public TimeSpan SearchScopeLifetime { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// The most <c>user:</c>, <c>org:</c> and <c>repo:</c> qualifiers in one search request. A text search that
    /// covers more is sent as several requests, so the request URL stays a reasonable length.
    /// </summary>
    public int MaxScopeQualifiers { get; init; } = 25;

    /// <summary>
    /// The longest Tammi waits for a rate limit to reset before retrying a request. When GitHub asks for a longer
    /// wait, the request fails with an <see cref="IntegrationRateLimitException"/> instead.
    /// </summary>
    public TimeSpan MaxRateLimitWait { get; init; } = TimeSpan.FromSeconds(10);
}
