using System.Text.Json;

namespace Deosrc.Tammi.Core.Integrations.GitHub;

/// <summary>
/// The GitHub user an access token belongs to.
/// </summary>
/// <param name="Login">The user name, e.g. <c>octocat</c>.</param>
/// <param name="Name">The display name, if the user has set one.</param>
public sealed record GitHubUser(string Login, string? Name);

// The parts of GitHub's REST API responses that Tammi reads. Property names are mapped from snake_case.

/// <param name="State"><c>open</c> or <c>closed</c>.</param>
/// <param name="StateReason">
/// Why a closed issue was closed: <c>completed</c>, <c>not_planned</c> or <c>duplicate</c> (or <c>reopened</c> for an open
/// one). Older issues don't have one.
/// </param>
internal sealed record GitHubIssue(
    int Number,
    string Title,
    string? Body,
    string HtmlUrl,
    string RepositoryUrl,
    JsonElement? PullRequest,
    DateTimeOffset? UpdatedAt = null,
    string? State = null,
    string? StateReason = null)
{
    /// <summary>
    /// GitHub's issue endpoints also return pull requests, which have a <c>pull_request</c> property.
    /// </summary>
    public bool IsPullRequest => PullRequest is { ValueKind: not JsonValueKind.Null };
}

internal sealed record GitHubSearchResults(int TotalCount, bool IncompleteResults, IReadOnlyList<GitHubIssue> Items);

internal sealed record GitHubRepository(
    string Name,
    GitHubAccount Owner,
    string HtmlUrl,
    string CloneUrl,
    string SshUrl,
    bool Private = false);

/// <param name="Login">The user or organisation name.</param>
/// <param name="Type"><c>User</c> or <c>Organization</c>.</param>
internal sealed record GitHubAccount(string Login, string? Type = null)
{
    public bool IsOrganization => string.Equals(Type, "Organization", StringComparison.OrdinalIgnoreCase);
}

/// <param name="Key">The public key, without its comment.</param>
internal sealed record GitHubSshKey(long Id, string Key, string? Title = null);

/// <summary>
/// The body of a request to add an SSH key to the user's account.
/// </summary>
internal sealed record GitHubNewSshKey(string Title, string Key);

/// <remarks>
/// Each of <see cref="Errors"/> is usually an object with a <c>message</c>, but can be a plain string.
/// </remarks>
internal sealed record GitHubError(string? Message, IReadOnlyList<JsonElement>? Errors)
{
    public IEnumerable<string> ErrorMessages =>
        (Errors ?? []).Select(error => error.ValueKind switch
        {
            JsonValueKind.String => error.GetString(),
            JsonValueKind.Object when error.TryGetProperty("message", out JsonElement message)
                && message.ValueKind == JsonValueKind.String => message.GetString(),
            _ => null,
        }).OfType<string>();
}
