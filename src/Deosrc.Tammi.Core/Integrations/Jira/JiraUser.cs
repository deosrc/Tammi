namespace TAMMi.Core.Integrations.Jira;

/// <summary>
/// The JIRA user the credentials belong to.
/// </summary>
/// <param name="DisplayName">The user's name as JIRA shows it.</param>
/// <param name="EmailAddress">The user's email address, if JIRA shares it.</param>
public sealed record JiraUser(string DisplayName, string? EmailAddress);
