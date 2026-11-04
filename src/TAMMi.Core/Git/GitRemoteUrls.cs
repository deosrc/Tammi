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
