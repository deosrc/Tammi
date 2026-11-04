namespace Deosrc.Tammi.Core.Integrations.GitHub;

/// <summary>
/// The fine-grained personal access token Tammi needs, as a link to GitHub's new-token page with its details filled
/// in. The user still chooses the token's owner and which repositories it can reach.
/// </summary>
/// <remarks>
/// GitHub reads the details from the link's query (see "Pre-filling fine-grained personal access token details using
/// URL parameters" in GitHub's documentation). GitHub Enterprise Server reads them from version 3.20, and ignores them
/// before that.
/// </remarks>
public static class GitHubTokenTemplate
{
    /// <summary>
    /// The repository permissions the token needs, all read-only. Contents is needed too: without it, GitHub's issue
    /// search leaves out private repositories' issues.
    /// </summary>
    public static IReadOnlyList<string> Permissions { get; } = ["issues", "contents", "metadata"];

    /// <summary>
    /// The link to github.com's new fine-grained token page.
    /// </summary>
    public static Uri GitHubComUrl { get; } = NewTokenUrl(new Uri("https://github.com/"));

    /// <summary>
    /// Returns the link to the new fine-grained token page on a GitHub server, with the token's name, description
    /// and <see cref="Permissions"/> filled in.
    /// </summary>
    /// <param name="webUrl">The server's web address, e.g. <c>https://github.com/</c>.</param>
    public static Uri NewTokenUrl(Uri webUrl)
    {
        ArgumentNullException.ThrowIfNull(webUrl);
        if (!webUrl.IsAbsoluteUri)
        {
            throw new ArgumentException("The web address must be absolute.", nameof(webUrl));
        }

        string[] query =
        [
            $"name={Uri.EscapeDataString(AppInfo.Name)}",
            $"description={Uri.EscapeDataString($"Lets {AppInfo.Name} read your issues to build workspaces.")}",
            .. Permissions.Select(permission => $"{permission}=read"),
        ];
        return new Uri($"{webUrl.GetLeftPart(UriPartial.Authority)}/settings/personal-access-tokens/new?"
            + string.Join('&', query));
    }
}
