using System.Text.Json;

namespace TAMMi.Core.Integrations.GitHub;

/// <summary>
/// The GitHub user an access token belongs to.
/// </summary>
/// <param name="Login">The user name, e.g. <c>octocat</c>.</param>
/// <param name="Name">The display name, if the user has set one.</param>
public sealed record GitHubUser(string Login, string? Name);

// The parts of GitHub's REST API responses that TAMMi reads. Property names are mapped from snake_case.

internal sealed record GitHubIssue(
    int Number,
    string Title,
    string? Body,
    string HtmlUrl,
    string RepositoryUrl,
    JsonElement? PullRequest)
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
    string SshUrl);

internal sealed record GitHubAccount(string Login);

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
