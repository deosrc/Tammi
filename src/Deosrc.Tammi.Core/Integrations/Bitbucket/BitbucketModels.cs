using System.Text.Json;
using System.Text.Json.Serialization;

namespace Deosrc.Tammi.Core.Integrations.Bitbucket;

/// <summary>
/// The parts of Bitbucket Cloud's REST API responses that Tammi reads. Property names are mapped from snake_case.
/// </summary>
internal static class BitbucketModels
{
    /// <summary>
    /// One page of a list. <paramref name="Next"/> is the URL of the following page, if there is one.
    /// </summary>
    public sealed record Page<T>(IReadOnlyList<T>? Values, string? Next);

    public sealed record Link(string? Href, string? Name);

    public sealed record Links(
        Link? Html,
        [property: JsonPropertyName("clone")] IReadOnlyList<Link>? CloneLinks);

    public sealed record Content(string? Raw);

    /// <summary>
    /// The user a token belongs to.
    /// </summary>
    public sealed record User(string? DisplayName, string? Nickname, string? AccountId);

    /// <param name="FullName">The workspace and repository slugs, e.g. <c>acme/api</c>.</param>
    /// <param name="HasIssues">Whether the issue tracker is enabled, or <see langword="null"/> if not said.</param>
    public sealed record Repository(string? FullName, string? Name, bool? HasIssues, Links? Links);

    public sealed record Workspace(string? Slug);

    /// <summary>
    /// A workspace the user can access, as <c>user/workspaces</c> lists them. Bitbucket nests the workspace in an
    /// access record, or lists the workspace itself, so both are read.
    /// </summary>
    public sealed record WorkspaceAccess(Workspace? Workspace, string? Slug)
    {
        public string? WorkspaceSlug => Workspace?.Slug ?? Slug;
    }

    public sealed record Issue(
        int Id,
        string? Title,
        Content? Content,
        string? State,
        DateTimeOffset? UpdatedOn,
        Links? Links);

    public sealed record Branch(string? Name);

    public sealed record PullRequestEnd(Branch? Branch, Repository? Repository);

    public sealed record Participant(string? Role, bool? Approved, string? State);

    public sealed record PullRequest(
        int Id,
        string? Title,
        string? State,
        bool? Draft,
        PullRequestEnd? Source,
        Links? Links,
        IReadOnlyList<Participant>? Participants);

    /// <param name="State"><c>SUCCESSFUL</c>, <c>FAILED</c>, <c>INPROGRESS</c> or <c>STOPPED</c>.</param>
    public sealed record CommitStatus(string? State);

    /// <param name="Detail">More about the error: text, or an object (e.g. the scopes a token lacks).</param>
    public sealed record ErrorBody(string? Message, JsonElement? Detail);

    public sealed record Error([property: JsonPropertyName("error")] ErrorBody? Body);
}
