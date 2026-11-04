namespace Tammi.Core.Tests.Integrations.GitHub;

/// <summary>
/// Answers GitHub API requests with recorded or faked responses, and records the requests.
/// </summary>
internal sealed class FakeGitHubApi() : FakeHttpApi(ApiUrl)
{
    public const string ApiUrl = "https://api.github.com/";

    /// <summary>
    /// Reads a response recorded from the real API, in <c>Responses/</c>.
    /// </summary>
    public static string Recorded(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Integrations", "GitHub", "Responses", name));

    /// <summary>
    /// The absolute URL of a path relative to the API URL, e.g. for a <c>Link</c> header.
    /// </summary>
    public static string Url(string pathAndQuery) => new Uri(new Uri(ApiUrl), pathAndQuery).AbsoluteUri;
}
