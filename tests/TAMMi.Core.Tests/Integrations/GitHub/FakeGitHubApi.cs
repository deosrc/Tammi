using System.Net;
using System.Text;

namespace TAMMi.Core.Tests.Integrations.GitHub;

/// <summary>
/// Answers GitHub API requests with recorded or faked responses, and records the requests.
/// </summary>
internal sealed class FakeGitHubApi : HttpMessageHandler
{
    public const string ApiUrl = "https://api.github.com/";

    private readonly Dictionary<string, Queue<Func<HttpResponseMessage>>> _responses = [];

    /// <summary>
    /// The requests received, in order.
    /// </summary>
    public List<RecordedRequest> Requests { get; } = [];

    /// <summary>
    /// Reads a response recorded from the real API, in <c>Responses/</c>.
    /// </summary>
    public static string Recorded(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Integrations", "GitHub", "Responses", name));

    /// <summary>
    /// Answers the next request for <paramref name="pathAndQuery"/>. Responses for the same path are used in the order
    /// they were added, and the last is reused.
    /// </summary>
    /// <param name="pathAndQuery">
    /// The request's path and query string relative to the API URL, e.g. <c>user/repos?per_page=100</c>.
    /// </param>
    /// <param name="json">The response body.</param>
    /// <param name="status">The response status.</param>
    /// <param name="headers">Response headers, e.g. <c>Link</c>.</param>
    public FakeGitHubApi Respond(
        string pathAndQuery,
        string json,
        HttpStatusCode status = HttpStatusCode.OK,
        params (string Name, string Value)[] headers)
    {
        pathAndQuery = Normalise(pathAndQuery);
        if (!_responses.TryGetValue(pathAndQuery, out Queue<Func<HttpResponseMessage>>? queue))
        {
            _responses[pathAndQuery] = queue = new Queue<Func<HttpResponseMessage>>();
        }

        queue.Enqueue(() =>
        {
            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            foreach ((string name, string value) in headers)
            {
                response.Headers.TryAddWithoutValidation(name, value);
            }

            return response;
        });
        return this;
    }

    /// <summary>
    /// Fails the next request for <paramref name="pathAndQuery"/> as if the network was down.
    /// </summary>
    public FakeGitHubApi Fail(string pathAndQuery)
    {
        _responses[Normalise(pathAndQuery)] = new Queue<Func<HttpResponseMessage>>(
            [() => throw new HttpRequestException("No such host is known.")]);
        return this;
    }

    /// <summary>
    /// The absolute URL of a path relative to the API URL, e.g. for a <c>Link</c> header.
    /// </summary>
    public static string Url(string pathAndQuery) => new Uri(new Uri(ApiUrl), pathAndQuery).AbsoluteUri;

    private static string Normalise(string pathAndQuery) => new Uri(new Uri(ApiUrl), pathAndQuery).PathAndQuery;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Uri url = request.RequestUri!;
        Requests.Add(new RecordedRequest(
            request.Method,
            url,
            request.Headers.Authorization?.ToString(),
            request.Headers.ToDictionary(header => header.Key, header => string.Join(", ", header.Value))));

        if (url.GetLeftPart(UriPartial.Authority) + "/" != ApiUrl
            || !_responses.TryGetValue(url.PathAndQuery, out Queue<Func<HttpResponseMessage>>? queue))
        {
            throw new InvalidOperationException($"No response is set up for {url}.");
        }

        Func<HttpResponseMessage> respond = queue.Count > 1 ? queue.Dequeue() : queue.Peek();
        return Task.FromResult(respond());
    }
}

internal sealed record RecordedRequest(
    HttpMethod Method,
    Uri Url,
    string? Authorization,
    IReadOnlyDictionary<string, string> Headers)
{
    /// <summary>
    /// The value of a query string parameter, unescaped.
    /// </summary>
    public string? Query(string name) =>
        Url.Query.TrimStart('?').Split('&')
            .Select(part => part.Split('=', 2))
            .Where(part => part[0] == name)
            .Select(part => Uri.UnescapeDataString(part.ElementAtOrDefault(1) ?? ""))
            .FirstOrDefault();
}
