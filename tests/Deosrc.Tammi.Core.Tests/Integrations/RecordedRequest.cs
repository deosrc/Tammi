namespace Deosrc.Tammi.Core.Tests.Integrations;

/// <summary>
/// A request a <see cref="FakeHttpApi"/> received.
/// </summary>
internal sealed record RecordedRequest(
    HttpMethod Method,
    Uri Url,
    string? Authorization,
    IReadOnlyDictionary<string, string> Headers)
{
    /// <summary>
    /// The request's body, or <see langword="null"/> if it has none.
    /// </summary>
    public string? Body { get; init; }

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
