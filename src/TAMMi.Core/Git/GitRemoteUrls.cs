namespace TAMMi.Core.Git;

/// <summary>
/// Reads parts of git remote URLs, in any of the forms git accepts: <c>https://host/owner/repo.git</c>,
/// <c>ssh://git@host/owner/repo</c>, <c>git@host:owner/repo.git</c> and local paths.
/// </summary>
public static class GitRemoteUrls
{
    /// <summary>
    /// Returns the owner of the repository a remote URL points at: the path's second-to-last part, e.g. <c>acme</c> for
    /// <c>git@github.com:acme/api.git</c> or <c>https://github.com/acme/api</c>.
    /// </summary>
    /// <returns>The owner, or <see langword="null"/> if the path has no part before the repository's name.</returns>
    public static string? GetOwner(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        string path = GetPath(url.Trim());
        string[] parts = path.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? parts[^2] : null;
    }

    /// <summary>
    /// Whether a remote URL points at the repository at <paramref name="repositoryUrl"/> (e.g. its web page or another
    /// clone URL). The host and path are compared, ignoring case, the scheme, user, port and a trailing <c>.git</c>, so
    /// <c>git@github.com:acme/api.git</c> matches <c>https://github.com/acme/api</c>.
    /// </summary>
    public static bool IsSameRepository(string? url, string? repositoryUrl) =>
        Normalise(url) is { } normalised
        && string.Equals(normalised, Normalise(repositoryUrl), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns <c>host/path</c> for a URL with a host, without a trailing <c>/</c> or <c>.git</c>, or
    /// <see langword="null"/> for a local path.
    /// </summary>
    private static string? Normalise(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        url = url.Trim();
        string? host = GetHost(url);
        if (string.IsNullOrEmpty(host))
        {
            return null;
        }

        string path = GetPath(url).Replace('\\', '/').Trim('/');
        if (path.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            path = path[..^".git".Length];
        }

        return path.Length == 0 ? null : $"{host}/{path}";
    }

    /// <summary>
    /// The host of a URL, without the user or port, or <see langword="null"/> for a local path.
    /// </summary>
    private static string? GetHost(string url)
    {
        string authority;
        int scheme = url.IndexOf("://", StringComparison.Ordinal);
        if (scheme > 0)
        {
            if (url[..scheme].Equals("file", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string rest = url[(scheme + 3)..];
            int slash = rest.IndexOf('/');
            authority = slash < 0 ? rest : rest[..slash];
        }
        else
        {
            int colon = url.IndexOf(':');
            int firstSlash = url.IndexOfAny(['/', '\\']);
            if (colon <= 1 || (firstSlash >= 0 && firstSlash < colon))
            {
                return null;
            }

            authority = url[..colon];
        }

        int at = authority.LastIndexOf('@');
        if (at >= 0)
        {
            authority = authority[(at + 1)..];
        }

        int port = authority.LastIndexOf(':');
        if (port >= 0 && !authority.EndsWith(']'))
        {
            authority = authority[..port];
        }

        return authority;
    }

    /// <summary>
    /// The path part of a remote URL, without a query or fragment.
    /// </summary>
    private static string GetPath(string url)
    {
        int scheme = url.IndexOf("://", StringComparison.Ordinal);
        if (scheme > 0)
        {
            // scheme://[user@]host[:port]/path
            string rest = url[(scheme + 3)..];
            int slash = rest.IndexOf('/');
            string path = slash < 0 ? string.Empty : rest[slash..];
            int end = path.IndexOfAny(['?', '#']);
            return end < 0 ? path : path[..end];
        }

        // scp-like syntax, [user@]host:path. Git treats it as a local path if there's a slash before the colon, and
        // a single letter before the colon is a Windows drive.
        int colon = url.IndexOf(':');
        if (colon > 1)
        {
            int firstSlash = url.IndexOfAny(['/', '\\']);
            if (firstSlash < 0 || firstSlash > colon)
            {
                return url[(colon + 1)..];
            }
        }

        return url;
    }
}
