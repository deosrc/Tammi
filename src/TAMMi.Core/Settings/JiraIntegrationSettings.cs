using System.Text.Json.Serialization;
using TAMMi.Core.Integrations.Jira;

namespace TAMMi.Core.Settings;

/// <summary>
/// A JIRA site the user has connected. Its token is kept in the credential store, not here.
/// </summary>
public sealed record JiraIntegrationSettings
{
    private const string IdPrefix = "jira";

    /// <summary>
    /// The integration's <see cref="Integrations.IIntegration.Id"/>, e.g. <c>jira-acme.atlassian.net</c>. It never
    /// changes once the integration is added.
    /// </summary>
    public string Id { get; init; } = "";

    /// <summary>
    /// The site's base URL (see <see cref="JiraIntegrationOptions.SiteUrl"/>).
    /// </summary>
    public Uri? SiteUrl { get; init; }

    /// <summary>
    /// Whether the site is JIRA Cloud or JIRA Server / Data Center.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<JiraDeployment>))]
    public JiraDeployment Deployment { get; init; } = JiraDeployment.Cloud;

    /// <summary>
    /// The email address of the user's Atlassian account, which JIRA Cloud needs with the API token.
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// The name shown to the user, e.g. <c>JIRA (acme.atlassian.net)</c>.
    /// </summary>
    [JsonIgnore]
    public string Name => SiteUrl is null ? "JIRA" : $"JIRA ({SiteUrl.Authority}{SiteUrl.AbsolutePath.TrimEnd('/')})";

    /// <summary>
    /// The settings for a new integration with the given site, and the ID that goes with it.
    /// </summary>
    public static JiraIntegrationSettings For(Uri siteUrl, JiraDeployment deployment, string? email)
    {
        ArgumentNullException.ThrowIfNull(siteUrl);
        return new JiraIntegrationSettings
        {
            Id = IntegrationIds.Create(IdPrefix, siteUrl.Authority + siteUrl.AbsolutePath),
            SiteUrl = siteUrl,
            Deployment = deployment,
            Email = deployment == JiraDeployment.Cloud && !string.IsNullOrWhiteSpace(email) ? email.Trim() : null,
        };
    }

    /// <summary>
    /// Reads a site URL the user typed, e.g. <c>acme.atlassian.net</c> or <c>https://jira.example.com/jira</c>. A
    /// JIRA Cloud site is always served from its root, so the path of a page URL pasted from it is dropped.
    /// </summary>
    /// <returns>The site URL, or <see langword="null"/> if the input isn't an HTTP or HTTPS URL.</returns>
    public static Uri? ParseSiteUrl(string? input)
    {
        if (IntegrationIds.ParseWebUrl(input) is not { } url)
        {
            return null;
        }

        return IsCloudHost(url)
            ? new Uri(url.GetLeftPart(UriPartial.Authority) + "/")
            : new Uri(url.GetLeftPart(UriPartial.Authority) + url.AbsolutePath.TrimEnd('/') + "/");
    }

    /// <summary>
    /// The deployment a site URL most likely has: Cloud for <c>*.atlassian.net</c>, otherwise Server / Data Center.
    /// </summary>
    public static JiraDeployment GuessDeployment(Uri siteUrl) =>
        IsCloudHost(siteUrl) ? JiraDeployment.Cloud : JiraDeployment.DataCenter;

    /// <summary>
    /// Whether the settings can be used: they have an ID and an HTTP or HTTPS site URL, and a JIRA Cloud site has an
    /// email address.
    /// </summary>
    public bool IsValid() =>
        !string.IsNullOrWhiteSpace(Id)
        && IntegrationIds.IsWebUrl(SiteUrl)
        && Enum.IsDefined(Deployment)
        && (Deployment != JiraDeployment.Cloud || !string.IsNullOrWhiteSpace(Email));

    /// <summary>
    /// The options a <see cref="JiraIntegration"/> is created with.
    /// </summary>
    public JiraIntegrationOptions ToOptions() => new()
    {
        Id = Id,
        Name = Name,
        SiteUrl = SiteUrl!,
        Deployment = Deployment,
        Email = Deployment == JiraDeployment.Cloud ? Email?.Trim() : null,
    };

    private static bool IsCloudHost(Uri url) =>
        url.Host.EndsWith(".atlassian.net", StringComparison.OrdinalIgnoreCase);
}
