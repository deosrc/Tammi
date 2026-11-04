namespace Deosrc.Tammi.Core.Integrations.Jira;

/// <summary>
/// The parts of a JIRA issue that Tammi reads, from REST API version 2.
/// </summary>
/// <param name="Key">The issue key, e.g. <c>ABC-123</c>. For a moved issue it's the new key.</param>
/// <param name="Fields">The fields Tammi asks for.</param>
internal sealed record JiraIssue(string Key, JiraIssueFields? Fields);
