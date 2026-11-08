using System.Text.Json;

namespace Deosrc.Tammi.Core.Integrations.GitHub;

/// <summary>
/// An error in an answer from GitHub's GraphQL API.
/// </summary>
/// <param name="Type">
/// GitHub's kind of error, e.g. <c>NOT_FOUND</c>, <c>FORBIDDEN</c> or <c>RATE_LIMITED</c>. Errors in the query itself
/// have none.
/// </param>
/// <param name="Path">The field the error is about, as names and list indexes.</param>
/// <param name="Message">What went wrong.</param>
internal sealed record GitHubGraphQlError(string? Type, IReadOnlyList<JsonElement>? Path, string? Message)
{
    /// <summary>
    /// The field's path as text, e.g. <c>repository.pullRequests</c>, with list indexes left out.
    /// </summary>
    public string PathText => string.Join('.',
        (Path ?? []).Where(part => part.ValueKind == JsonValueKind.String).Select(part => part.GetString()));
}
