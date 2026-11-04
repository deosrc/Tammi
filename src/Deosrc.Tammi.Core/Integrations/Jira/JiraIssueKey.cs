using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace TAMMi.Core.Integrations.Jira;

/// <summary>
/// Reads JIRA issue keys, e.g. <c>ABC-123</c>, which are the <see cref="TaskInfo.Key"/>s of JIRA tasks.
/// </summary>
internal static partial class JiraIssueKey
{
    /// <summary>
    /// Reads an issue key. JIRA accepts keys in any case, so the key is returned in upper case, as JIRA shows it.
    /// </summary>
    public static bool TryParse(string? text, [NotNullWhen(true)] out string? key)
    {
        string trimmed = text?.Trim() ?? "";
        key = KeyPattern().IsMatch(trimmed) ? trimmed.ToUpperInvariant() : null;
        return key is not null;
    }

    /// <summary>
    /// Reads the issue a URL on the site points at: an issue's page (<c>…/browse/ABC-123</c>), or a board or
    /// search with the issue selected (<c>…?selectedIssue=ABC-123</c>).
    /// </summary>
    /// <param name="text">The URL.</param>
    /// <param name="siteUrl">The site's base URL. URLs on other sites aren't read.</param>
    /// <param name="key">The issue key.</param>
    public static bool TryParseUrl(string? text, Uri siteUrl, [NotNullWhen(true)] out string? key)
    {
        key = null;
        if (!Uri.TryCreate(text?.Trim(), UriKind.Absolute, out Uri? url)
            || url.Scheme is not ("https" or "http")
            || Uri.Compare(url, siteUrl, UriComponents.HostAndPort, UriFormat.Unescaped,
                StringComparison.OrdinalIgnoreCase) != 0)
        {
            return false;
        }

        string sitePath = siteUrl.AbsolutePath.TrimEnd('/') + "/";
        if (!url.AbsolutePath.StartsWith(sitePath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string[] segments = url.AbsolutePath[sitePath.Length..].Trim('/').Split('/');
        if (segments is ["browse", var browsed])
        {
            return TryParse(Uri.UnescapeDataString(browsed), out key);
        }

        string? selected = url.Query.TrimStart('?').Split('&')
            .Select(part => part.Split('=', 2))
            .Where(part => part is ["selectedIssue", _])
            .Select(part => Uri.UnescapeDataString(part[1]))
            .FirstOrDefault();
        return TryParse(selected, out key);
    }

    // A project key (a letter, then letters, digits or underscores) and the issue's number.
    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]*-[1-9][0-9]*$")]
    private static partial Regex KeyPattern();
}
