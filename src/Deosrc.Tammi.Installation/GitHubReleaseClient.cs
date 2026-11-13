using System.Net.Http.Headers;
using System.Text.Json;

namespace Deosrc.Tammi.Installation;

/// <summary>
/// Finds Tammi releases on GitHub.
/// </summary>
public sealed class GitHubReleaseClient
{
    /// <summary>
    /// The repository releases are published to.
    /// </summary>
    public const string Repository = "deosrc/Tammi";

    private readonly HttpClient _http;
    private readonly Uri _releasesUrl;

    /// <param name="http">The client to send requests with.</param>
    /// <param name="apiBase">The GitHub API's address. Set to test against another server.</param>
    public GitHubReleaseClient(HttpClient http, Uri? apiBase = null)
    {
        _http = http;
        _releasesUrl = new Uri(apiBase ?? new Uri("https://api.github.com/"), $"repos/{Repository}/releases?per_page=30");
    }

    /// <summary>
    /// Gets the newest release, ignoring drafts.
    /// </summary>
    /// <param name="includePreReleases">Whether a pre-release can be the newest.</param>
    /// <returns>The release, or <see langword="null"/> if there's none.</returns>
    /// <exception cref="HttpRequestException">GitHub couldn't be reached or refused the request.</exception>
    public async Task<ReleaseInfo?> GetLatestAsync(bool includePreReleases, CancellationToken cancellationToken = default) =>
        (await ListAsync(cancellationToken))
            .Where(release => includePreReleases || !release.IsPreRelease)
            .OrderByDescending(release => release.Version)
            .FirstOrDefault();

    /// <summary>
    /// Gets the release with a tag.
    /// </summary>
    /// <returns>The release, or <see langword="null"/> if there's none.</returns>
    public async Task<ReleaseInfo?> GetByTagAsync(string tag, CancellationToken cancellationToken = default) =>
        (await ListAsync(cancellationToken)).FirstOrDefault(release =>
            string.Equals(release.Tag, tag, StringComparison.OrdinalIgnoreCase)
            || string.Equals(release.Tag.TrimStart('v'), tag.TrimStart('v'), StringComparison.OrdinalIgnoreCase));

    private async Task<IReadOnlyList<ReleaseInfo>> ListAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _releasesUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return ParseReleases(document.RootElement);
    }

    internal static IReadOnlyList<ReleaseInfo> ParseReleases(JsonElement array)
    {
        var releases = new List<ReleaseInfo>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.TryGetProperty("draft", out var draft) && draft.GetBoolean())
            {
                continue;
            }

            var tag = item.GetProperty("tag_name").GetString();
            if (!PackageVersion.TryParse(tag, out var version))
            {
                continue;
            }

            var assets = item.GetProperty("assets").EnumerateArray()
                .Select(asset => new ReleaseAsset(
                    asset.GetProperty("name").GetString()!,
                    new Uri(asset.GetProperty("browser_download_url").GetString()!),
                    asset.GetProperty("size").GetInt64()))
                .ToList();
            releases.Add(new ReleaseInfo(tag!, version, new Uri(item.GetProperty("html_url").GetString()!), assets));
        }

        return releases;
    }
}
