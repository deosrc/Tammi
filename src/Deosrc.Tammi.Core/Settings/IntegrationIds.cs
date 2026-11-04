using System.Text;

namespace TAMMi.Core.Settings;

/// <summary>
/// Builds the <see cref="Integrations.IIntegration.Id"/> of a configured integration from the service it connects
/// to, so connecting the same service again (e.g. after removing it) gives the same ID, and workspace tasks that name
/// it as their source find it again.
/// </summary>
internal static class IntegrationIds
{
    /// <summary>
    /// Joins a prefix and a slug of <paramref name="value"/>, e.g. <c>jira-acme.atlassian.net</c>.
    /// </summary>
    public static string Create(string prefix, string value)
    {
        var slug = new StringBuilder(value.Length);
        foreach (char c in value.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(c) || c == '.')
            {
                slug.Append(c);
            }
            else if (slug.Length > 0 && slug[^1] != '-')
            {
                slug.Append('-');
            }
        }

        string trimmed = slug.ToString().Trim('-', '.');
        return trimmed.Length == 0 ? prefix : $"{prefix}-{trimmed}";
    }

    /// <summary>
    /// Reads a URL the user typed. A URL without a scheme is taken to be HTTPS. The query and fragment are dropped.
    /// </summary>
    /// <returns>The URL, or <see langword="null"/> if it isn't an HTTP or HTTPS URL.</returns>
    public static Uri? ParseWebUrl(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        string text = input.Trim();
        if (!text.Contains("://", StringComparison.Ordinal))
        {
            text = "https://" + text;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out Uri? url)
            || url.Scheme is not ("https" or "http")
            || string.IsNullOrEmpty(url.Host))
        {
            return null;
        }

        return new Uri(url.GetLeftPart(UriPartial.Path));
    }

    /// <summary>
    /// Whether <paramref name="url"/> is an absolute HTTP or HTTPS URL.
    /// </summary>
    public static bool IsWebUrl(Uri? url) =>
        url is { IsAbsoluteUri: true, Scheme: "https" or "http" } && !string.IsNullOrEmpty(url.Host);

    /// <summary>
    /// <paramref name="url"/> with a trailing <c>/</c>, so relative paths resolve beneath it.
    /// </summary>
    public static Uri WithTrailingSlash(Uri url) =>
        url.AbsoluteUri.EndsWith('/') ? url : new Uri(url.AbsoluteUri + "/");
}
