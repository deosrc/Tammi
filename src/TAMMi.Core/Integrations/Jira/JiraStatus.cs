namespace TAMMi.Core.Integrations.Jira;

/// <summary>
/// An issue's status, from REST API version 2.
/// </summary>
/// <param name="Name">The status's name, e.g. <c>In Review</c>.</param>
/// <param name="StatusCategory">The category JIRA puts the status in.</param>
internal sealed record JiraStatus(string? Name, JiraStatusCategory? StatusCategory);
