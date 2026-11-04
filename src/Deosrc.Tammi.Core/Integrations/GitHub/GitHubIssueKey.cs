using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Deosrc.Tammi.Core.Integrations.GitHub;

/// <summary>
/// Identifies a GitHub issue. Its <see cref="TaskInfo.Key"/> form is <c>owner/repo#number</c>, as GitHub writes
/// issue references.
/// </summary>
internal sealed partial record GitHubIssueKey(string Owner, string Repository, int Number)
{
    public override string ToString() => $"{Owner}/{Repository}#{Number.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>
    /// Reads a key in the form <c>owner/repo#number</c>.
    /// </summary>
    public static bool TryParse(string? text, [NotNullWhen(true)] out GitHubIssueKey? key)
    {
        Match match = KeyPattern().Match(text?.Trim() ?? "");
        key = match.Success
            ? Create(match.Groups["owner"].Value, match.Groups["repo"].Value, match.Groups["number"].Value)
            : null;
        return key is not null;
    }

    /// <summary>
    /// Reads an issue's web page URL, e.g. <c>https://github.com/acme/api/issues/42</c>.
    /// </summary>
    /// <param name="text">The URL.</param>
    /// <param name="webHost">The host GitHub's web pages are served from, e.g. <c>github.com</c>.</param>
    /// <param name="key">The issue.</param>
    public static bool TryParseUrl(string? text, string webHost, [NotNullWhen(true)] out GitHubIssueKey? key)
    {
        key = null;
        if (!Uri.TryCreate(text?.Trim(), UriKind.Absolute, out Uri? url)
            || url.Scheme is not ("https" or "http")
            || !string.Equals(url.Host, webHost, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string[] segments = url.AbsolutePath.Trim('/').Split('/');
        if (segments is [var owner, var repository, "issues", var number])
        {
            key = Create(Uri.UnescapeDataString(owner), Uri.UnescapeDataString(repository), number);
        }

        return key is not null;
    }

    /// <summary>
    /// Reads the owner and name from an API repository URL, e.g. <c>https://api.github.com/repos/acme/api</c>.
    /// </summary>
    public static (string Owner, string Repository)? ParseRepositoryApiUrl(string repositoryUrl)
    {
        if (!Uri.TryCreate(repositoryUrl, UriKind.Absolute, out Uri? url))
        {
            return null;
        }

        string[] segments = url.AbsolutePath.Trim('/').Split('/');
        return segments is [.., "repos", var owner, var repository]
            ? (Uri.UnescapeDataString(owner), Uri.UnescapeDataString(repository))
            : null;
    }

    private static GitHubIssueKey? Create(string owner, string repository, string number) =>
        OwnerPattern().IsMatch(owner)
        && RepositoryPattern().IsMatch(repository)
        && repository is not ("." or "..")
        && int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out int value)
        && value > 0
            ? new GitHubIssueKey(owner, repository, value)
            : null;

    [GeneratedRegex(@"^(?<owner>[^/#\s]+)/(?<repo>[^/#\s]+)#(?<number>[0-9]+)$")]
    private static partial Regex KeyPattern();

    // GitHub user and organisation names: letters, digits and hyphens.
    [GeneratedRegex("^[A-Za-z0-9-]+$")]
    private static partial Regex OwnerPattern();

    // GitHub repository names: letters, digits, hyphens, underscores and dots.
    [GeneratedRegex(@"^[A-Za-z0-9._-]+$")]
    private static partial Regex RepositoryPattern();
}
