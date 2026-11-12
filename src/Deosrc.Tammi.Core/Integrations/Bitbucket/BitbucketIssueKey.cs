using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Deosrc.Tammi.Core.Integrations.Bitbucket;

/// <summary>
/// Identifies a Bitbucket issue. Its <see cref="TaskInfo.Key"/> form is <c>workspace/repo#number</c>.
/// </summary>
internal sealed partial record BitbucketIssueKey(string Workspace, string Repository, int Number)
{
    public override string ToString() =>
        $"{Workspace}/{Repository}#{Number.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>
    /// Reads a key in the form <c>workspace/repo#number</c>.
    /// </summary>
    public static bool TryParse(string? text, [NotNullWhen(true)] out BitbucketIssueKey? key)
    {
        Match match = KeyPattern().Match(text?.Trim() ?? "");
        key = match.Success
            ? Create(match.Groups["workspace"].Value, match.Groups["repo"].Value, match.Groups["number"].Value)
            : null;
        return key is not null;
    }

    /// <summary>
    /// Reads an issue's web page URL, e.g. <c>https://bitbucket.org/acme/api/issues/42/fix-login</c>.
    /// </summary>
    public static bool TryParseUrl(string? text, string webHost, [NotNullWhen(true)] out BitbucketIssueKey? key)
    {
        key = null;
        if (!Uri.TryCreate(text?.Trim(), UriKind.Absolute, out Uri? url)
            || url.Scheme is not ("https" or "http")
            || !string.Equals(url.Host, webHost, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string[] segments = url.AbsolutePath.Trim('/').Split('/');
        if (segments is [var workspace, var repository, "issues", var number, ..]
            && segments.Length <= 5)
        {
            key = Create(Uri.UnescapeDataString(workspace), Uri.UnescapeDataString(repository), number);
        }

        return key is not null;
    }

    private static BitbucketIssueKey? Create(string workspace, string repository, string number) =>
        SlugPattern().IsMatch(workspace)
        && SlugPattern().IsMatch(repository)
        && repository is not ("." or "..")
        && int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out int value)
        && value > 0
            ? new BitbucketIssueKey(workspace, repository, value)
            : null;

    [GeneratedRegex(@"^(?<workspace>[^/#\s]+)/(?<repo>[^/#\s]+)#(?<number>[0-9]+)$")]
    private static partial Regex KeyPattern();

    // Workspace and repository slugs: letters, digits, hyphens, underscores and dots.
    [GeneratedRegex(@"^[A-Za-z0-9._-]+$")]
    private static partial Regex SlugPattern();
}
