namespace Deosrc.Tammi.Core.Tests.Integrations.Bitbucket;

/// <summary>
/// Answers Bitbucket Cloud REST API requests with faked responses, and records the requests.
/// </summary>
internal sealed class FakeBitbucketApi() : FakeHttpApi("https://api.bitbucket.org/2.0/")
{
    /// <summary>
    /// Reads a faked response, in <c>Responses/</c>.
    /// </summary>
    public static string Response(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Integrations", "Bitbucket", "Responses", name));
}
