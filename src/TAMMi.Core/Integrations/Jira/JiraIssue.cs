namespace TAMMi.Core.Integrations.Jira;

/// <summary>
/// The parts of a JIRA issue that TAMMi reads, from REST API version 2.
/// </summary>
/// <param name="Key">The issue key, e.g. <c>ABC-123</c>. For a moved issue it's the new key.</param>
/// <param name="Fields">The fields TAMMi asks for.</param>
internal sealed record JiraIssue(string Key, JiraIssueFields? Fields);
