namespace Deosrc.Tammi.Core.Integrations.Jira;

/// <summary>
/// How a <see cref="JiraIntegration"/> connects to a JIRA site.
/// </summary>
public sealed record JiraIntegrationOptions
{
    /// <summary>
    /// The integration's <see cref="IIntegration.Id"/>. It also names its secret in the credential store. The user can
    /// connect several JIRA sites, so there's no default.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The integration's <see cref="IIntegration.Name"/>. It defaults to <c>JIRA (host)</c>, e.g.
    /// <c>JIRA (acme.atlassian.net)</c>.
    /// </summary>
    public string Name
    {
        get => field ?? $"JIRA ({SiteUrl?.Host})";
        init;
    }

    /// <summary>
    /// The site's base URL, e.g. <c>https://acme.atlassian.net</c>, or <c>https://jira.example.com/jira</c> for a
    /// Data Center site served under a path.
    /// </summary>
    public required Uri SiteUrl { get; init; }

    /// <summary>
    /// Whether the site is JIRA Cloud or JIRA Server / Data Center.
    /// </summary>
    public JiraDeployment Deployment { get; init; } = JiraDeployment.Cloud;

    /// <summary>
    /// The email address of the user's Atlassian account. JIRA Cloud needs it with the API token. Data Center
    /// doesn't use it. It isn't secret, so it's a setting rather than part of the stored secret.
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// How many issues to ask for in each page of a search, up to 100.
    /// </summary>
    public int PageSize { get; init; } = 50;

    /// <summary>
    /// The most tasks a search returns.
    /// </summary>
    public int MaxSearchResults { get; init; } = 100;

    /// <summary>
    /// The longest Tammi waits for a rate limit to reset before retrying a request. When JIRA asks for a longer
    /// wait, the request fails with an <see cref="IntegrationRateLimitException"/> instead.
    /// </summary>
    public TimeSpan MaxRateLimitWait { get; init; } = TimeSpan.FromSeconds(10);
}
