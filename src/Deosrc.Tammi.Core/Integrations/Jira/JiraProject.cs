namespace Deosrc.Tammi.Core.Integrations.Jira;

/// <summary>
/// A JIRA project, as far as completing an issue number into an issue key needs.
/// </summary>
/// <param name="Key">The project's key, e.g. <c>ABC</c>.</param>
public sealed record JiraProject(string Key);
