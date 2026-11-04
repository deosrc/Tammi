using System.Text.Json.Serialization;
using Deosrc.Tammi.Core.Integrations.GitHub;

namespace Deosrc.Tammi.Core.Settings;

/// <summary>
/// A GitHub integration the user has connected: github.com, or a GitHub Enterprise Server. Its access token is kept
/// in the credential store, not here.
/// </summary>
public sealed record GitHubIntegrationSettings
{
    /// <summary>
    /// The integration's <see cref="Integrations.IIntegration.Id"/>: <c>github</c> for github.com, or
    /// <c>github-&lt;host&gt;</c> for a GitHub Enterprise Server. It never changes once the integration is added.
    /// </summary>
    public string Id { get; init; } = "";

    /// <summary>
    /// The REST API to call (see <see cref="GitHubIntegrationOptions.ApiUrl"/>).
    /// </summary>
    public Uri ApiUrl { get; init; } = GitHubIntegrationOptions.GitHubComApiUrl;

    /// <summary>
    /// Whether this is github.com rather than a GitHub Enterprise Server.
    /// </summary>
    [JsonIgnore]
    public bool IsGitHubCom => string.Equals(ApiUrl?.Host, "api.github.com", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The name shown to the user: <c>GitHub</c>, or <c>GitHub (host)</c> for a GitHub Enterprise Server.
    /// </summary>
    [JsonIgnore]
    public string Name => IsGitHubCom ? "GitHub" : $"GitHub ({ApiUrl?.Authority})";

    /// <summary>
    /// The settings for a new integration with <paramref name="apiUrl"/>, and the ID that goes with it.
    /// </summary>
    public static GitHubIntegrationSettings For(Uri apiUrl)
    {
        ArgumentNullException.ThrowIfNull(apiUrl);
        var settings = new GitHubIntegrationSettings { ApiUrl = IntegrationIds.WithTrailingSlash(apiUrl) };
        return settings with
        {
            Id = settings.IsGitHubCom
                ? GitHubIntegrationOptions.DefaultId
                : IntegrationIds.Create(GitHubIntegrationOptions.DefaultId, apiUrl.Authority),
        };
    }

    /// <summary>
    /// Reads a GitHub Enterprise Server URL the user typed. Its API URL (e.g. <c>https://github.example.com/api/v3/</c>)
    /// or its web address (e.g. <c>github.example.com</c>, to which <c>api/v3/</c> is added) are both accepted. A
    /// github.com URL gives github.com's API URL.
    /// </summary>
    /// <returns>The API URL, or <see langword="null"/> if the input isn't an HTTP or HTTPS URL.</returns>
    public static Uri? ParseApiUrl(string? input)
    {
        if (IntegrationIds.ParseWebUrl(input) is not { } url)
        {
            return null;
        }

        if (url.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            || url.Host.Equals("api.github.com", StringComparison.OrdinalIgnoreCase))
        {
            return GitHubIntegrationOptions.GitHubComApiUrl;
        }

        url = IntegrationIds.WithTrailingSlash(url);
        return url.AbsolutePath == "/" ? new Uri(url, "api/v3/") : url;
    }

    /// <summary>
    /// Whether the settings can be used, i.e. they have an ID and an HTTP or HTTPS API URL.
    /// </summary>
    public bool IsValid() => !string.IsNullOrWhiteSpace(Id) && IntegrationIds.IsWebUrl(ApiUrl);

    /// <summary>
    /// The options a <see cref="GitHubIntegration"/> is created with.
    /// </summary>
    public GitHubIntegrationOptions ToOptions() => new() { Id = Id, Name = Name, ApiUrl = ApiUrl };
}
