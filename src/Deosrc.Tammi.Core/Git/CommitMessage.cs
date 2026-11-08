using System.Text.RegularExpressions;

namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// Reads parts of a commit's message.
/// </summary>
public static partial class CommitMessage
{
    /// <summary>
    /// The people a message's <c>Co-authored-by:</c> trailers name, in the order they're written, without repeats
    /// (by email, ignoring case). Trailers that don't have a name and an email in angle brackets are skipped.
    /// </summary>
    public static IReadOnlyList<CommitCoAuthor> CoAuthors(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var coAuthors = new List<CommitCoAuthor>();
        foreach (Match match in CoAuthorPattern().Matches(message))
        {
            string email = match.Groups["email"].Value.Trim();
            if (seen.Add(email))
            {
                coAuthors.Add(new CommitCoAuthor(match.Groups["name"].Value.Trim(), email));
            }
        }

        return coAuthors;
    }

    [GeneratedRegex(@"^[ \t]*Co-authored-by:[ \t]*(?<name>[^<\r\n]+?)[ \t]*<(?<email>[^>\r\n]+)>[ \t]*\r?$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex CoAuthorPattern();
}
