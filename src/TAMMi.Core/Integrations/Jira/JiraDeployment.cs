namespace TAMMi.Core.Integrations.Jira;

/// <summary>
/// The kind of JIRA site an integration connects to. They authenticate and search differently.
/// </summary>
public enum JiraDeployment
{
    /// <summary>
    /// A JIRA Cloud site, e.g. <c>https://acme.atlassian.net</c>. It authenticates with the user's email address and an
    /// API token.
    /// </summary>
    Cloud,

    /// <summary>
    /// A JIRA Server or Data Center site, e.g. <c>https://jira.example.com</c>. It authenticates with a personal access
    /// token, available from JIRA 8.14.
    /// </summary>
    DataCenter,
}
