namespace Deosrc.Tammi.Core.Integrations.Jira;

/// <summary>
/// A page of JIRA Cloud's issue search (<c>search/jql</c>), which pages with a token.
/// </summary>
/// <param name="Issues">The issues on the page.</param>
/// <param name="NextPageToken">Asks for the next page. <see langword="null"/> on the last page.</param>
/// <param name="IsLast">Whether this is the last page.</param>
internal sealed record JiraCloudSearchResults(
    IReadOnlyList<JiraIssue>? Issues,
    string? NextPageToken,
    bool? IsLast);
