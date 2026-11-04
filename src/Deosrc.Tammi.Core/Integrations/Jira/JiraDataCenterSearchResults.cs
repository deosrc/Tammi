namespace Deosrc.Tammi.Core.Integrations.Jira;

/// <summary>
/// A page of JIRA Server and Data Center's issue search (<c>search</c>), which pages by position.
/// </summary>
/// <param name="StartAt">The position of the page's first issue in the results.</param>
/// <param name="Total">How many issues match the search.</param>
/// <param name="Issues">The issues on the page.</param>
internal sealed record JiraDataCenterSearchResults(int StartAt, int Total, IReadOnlyList<JiraIssue>? Issues);
