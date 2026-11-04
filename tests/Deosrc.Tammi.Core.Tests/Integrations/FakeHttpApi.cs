using System.Net;
using System.Text;

namespace Tammi.Core.Tests.Integrations;

/// <summary>
/// Answers an integration's API requests with recorded or faked responses, and records the requests.
/// </summary>
/// <param name="baseUrl">
/// The API's base URL, ending in <c>/</c>, that relative paths are resolved against. Requests without a response fail.
/// </param>
internal class FakeHttpApi(string baseUrl) : HttpMessageHandler
{
    private readonly Uri _baseUrl = new(baseUrl);
    private readonly Dictionary<string, Queue<Func<HttpResponseMessage>>> _responses = [];

    /// <summary>
    /// The requests received, in order.
    /// </summary>
    public List<RecordedRequest> Requests { get; } = [];

    /// <summary>
    /// Answers the next request for <paramref name="pathAndQuery"/>. Responses for the same path are used in the order
    /// they were added, and the last is reused.
    /// </summary>
    /// <param name="pathAndQuery">
    /// The request's path and query string relative to the base URL, e.g. <c>user/repos?per_page=100</c>, or an
    /// absolute URL for a request to another host.
    /// </param>
    /// <param name="json">The response body.</param>
    /// <param name="status">The response status.</param>
    /// <param name="headers">Response headers, e.g. <c>Link</c>.</param>
    public FakeHttpApi Respond(
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
    public FakeHttpApi Fail(string pathAndQuery)
    {
        _responses[Normalise(pathAndQuery)] = new Queue<Func<HttpResponseMessage>>(
            [() => throw new HttpRequestException("No such host is known.")]);
        return this;
    }

    private string Normalise(string pathAndQuery) => new Uri(_baseUrl, pathAndQuery).AbsoluteUri;

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

        if (!_responses.TryGetValue(url.AbsoluteUri, out Queue<Func<HttpResponseMessage>>? queue))
        {
            throw new InvalidOperationException($"No response is set up for {url}.");
        }

        Func<HttpResponseMessage> respond = queue.Count > 1 ? queue.Dequeue() : queue.Peek();
        return Task.FromResult(respond());
    }
}
