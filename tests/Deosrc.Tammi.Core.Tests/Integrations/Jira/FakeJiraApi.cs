namespace TAMMi.Core.Tests.Integrations.Jira;

/// <summary>
/// Answers JIRA REST API requests with faked responses, and records the requests.
/// </summary>
/// <param name="siteUrl">The site's base URL, ending in <c>/</c>.</param>
internal sealed class FakeJiraApi(string siteUrl = FakeJiraApi.CloudSiteUrl) : FakeHttpApi(siteUrl + "rest/api/2/")
{
    public const string CloudSiteUrl = "https://acme.atlassian.net/";

    /// <summary>
    /// Reads a faked response, in <c>Responses/</c>.
    /// </summary>
    public static string Response(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Integrations", "Jira", "Responses", name));
}
