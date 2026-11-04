using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Deosrc.Tammi.Core.Integrations.GitHub;

/// <summary>
/// Calls GitHub's REST API: authenticates each request, follows pages and waits out short rate limits. Errors become
/// <see cref="IntegrationException"/>s.
/// </summary>
internal sealed partial class GitHubApiClient(
    GitHubIntegrationOptions options,
    IntegrationCredentials credentials,
    HttpClient httpClient,
    TimeProvider timeProvider)
{
    /// <summary>
    /// How often a request is retried after a rate limit, so a limit that keeps coming back can't retry forever.
    /// </summary>
    internal const int MaxRateLimitRetries = 3;

    /// <summary>
    /// GitHub asks clients to wait at least a minute after a secondary rate limit that doesn't say how long to wait.
    /// </summary>
    internal static readonly TimeSpan SecondaryRateLimitWait = TimeSpan.FromMinutes(1);

    private const string ApiVersion = "2022-11-28";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private readonly Uri _apiUrl = options.ApiUrl.AbsoluteUri.EndsWith('/')
        ? options.ApiUrl
        : new Uri(options.ApiUrl.AbsoluteUri + "/");

    /// <summary>
    /// Reads one resource.
    /// </summary>
    /// <param name="path">The path relative to the API URL, with any query string, e.g. <c>user</c>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The resource, or <see langword="null"/> if GitHub says it doesn't exist or is gone.</returns>
    public async Task<T?> GetAsync<T>(string path, CancellationToken cancellationToken)
        where T : class
    {
        using HttpResponseMessage? response = await SendAsync(
            new Uri(_apiUrl, path), allowNotFound: true, cancellationToken).ConfigureAwait(false);
        return response is null ? null : await ReadAsync<T>(response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Requests a resource only to find out whether the token may read it.
    /// </summary>
    /// <param name="path">The path relative to the API URL, with any query string.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>
    /// The response's status. It doesn't throw for an error status, except when GitHub rejects the token or a rate
    /// limit is reached.
    /// </returns>
    public async Task<HttpStatusCode> GetStatusAsync(string path, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = (await SendAsync(
            new Uri(_apiUrl, path), allowNotFound: false, cancellationToken, returnFailures: true)
            .ConfigureAwait(false))!;
        return response.StatusCode;
    }

    /// <summary>
    /// Reads every page of a list, following GitHub's <c>Link</c> header.
    /// </summary>
    /// <param name="path">The path of the first page, relative to the API URL, with any query string.</param>
    /// <param name="items">Gets the items from a page.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async IAsyncEnumerable<TItem> GetPagesAsync<TPage, TItem>(
        string path,
        Func<TPage, IEnumerable<TItem>> items,
        [EnumeratorCancellation] CancellationToken cancellationToken)
        where TPage : class
    {
        Uri? next = new(_apiUrl, path);
        while (next is not null)
        {
            TPage page;
            using (HttpResponseMessage response = (await SendAsync(next, allowNotFound: false, cancellationToken)
                .ConfigureAwait(false))!)
            {
                page = await ReadAsync<TPage>(response, cancellationToken).ConfigureAwait(false);
                next = NextPage(response);
            }

            foreach (TItem item in items(page))
            {
                yield return item;
            }
        }
    }

    private async Task<HttpResponseMessage?> SendAsync(
        Uri url,
        bool allowNotFound,
        CancellationToken cancellationToken,
        bool returnFailures = false)
    {
        string token = await credentials.GetRequiredAsync(cancellationToken).ConfigureAwait(false);
        for (int attempt = 0; ; attempt++)
        {
            HttpResponseMessage response = await SendOnceAsync(url, token, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            bool keep = false;
            try
            {
                if (allowNotFound && response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
                {
                    return null;
                }

                string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new IntegrationAuthenticationException(
                        options.Id,
                        "GitHub rejected the access token. It may have expired or been revoked. "
                        + "Authenticate again in Settings → Integrations.");
                }

                if (RateLimitRetryAt(response, body) is not { } retryAt)
                {
                    if (returnFailures)
                    {
                        keep = true;
                        return response;
                    }

                    throw Error(response, body);
                }

                TimeSpan wait = retryAt - timeProvider.GetUtcNow();
                if (attempt >= MaxRateLimitRetries || wait > options.MaxRateLimitWait)
                {
                    throw new IntegrationRateLimitException(
                        options.Id,
                        $"GitHub's rate limit was reached. Try again after {retryAt.ToLocalTime():t}.",
                        retryAt);
                }

                if (wait > TimeSpan.Zero)
                {
                    await Task.Delay(wait, timeProvider, cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                if (!keep)
                {
                    response.Dispose();
                }
            }
        }
    }

    private async Task<HttpResponseMessage> SendOnceAsync(Uri url, string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue(AppInfo.Name, AppInfo.Version));
        request.Headers.Add("X-GitHub-Api-Version", ApiVersion);

        try
        {
            return await httpClient
                .SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            throw new IntegrationException($"GitHub couldn't be reached: {exception.Message}", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new IntegrationException("GitHub didn't respond in time.", exception);
        }
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            Stream content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (content.ConfigureAwait(false))
            {
                return await JsonSerializer.DeserializeAsync<T>(content, JsonOptions, cancellationToken)
                        .ConfigureAwait(false)
                    ?? throw new JsonException("The response is empty.");
            }
        }
        catch (JsonException exception)
        {
            throw new IntegrationException($"GitHub returned a response Tammi couldn't read: {exception.Message}",
                exception);
        }
    }

    /// <summary>
    /// When a rate-limited request can be tried again, or <see langword="null"/> if the response isn't a rate limit.
    /// </summary>
    /// <remarks>
    /// GitHub signals its primary rate limit with <c>x-ratelimit-remaining: 0</c> and the reset time, and secondary
    /// rate limits with <c>retry-after</c> or only a message. Both use status 403 or 429.
    /// </remarks>
    private DateTimeOffset? RateLimitRetryAt(HttpResponseMessage response, string body)
    {
        if (response.StatusCode is not (HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests))
        {
            return null;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        if (response.Headers.RetryAfter is { } retryAfter)
        {
            return retryAfter.Delta is { } delta ? now + delta : retryAfter.Date ?? now;
        }

        if (Header(response, "x-ratelimit-remaining") == "0")
        {
            return long.TryParse(Header(response, "x-ratelimit-reset"), NumberStyles.None,
                CultureInfo.InvariantCulture, out long reset)
                ? DateTimeOffset.FromUnixTimeSeconds(reset)
                : now + SecondaryRateLimitWait;
        }

        return response.StatusCode == HttpStatusCode.TooManyRequests
            || ErrorMessage(body)?.Contains("rate limit", StringComparison.OrdinalIgnoreCase) == true
                ? now + SecondaryRateLimitWait
                : null;
    }

    private static IntegrationException Error(HttpResponseMessage response, string body)
    {
        var status = $"{(int)response.StatusCode} {response.ReasonPhrase}".TrimEnd();
        string? message = ErrorMessage(body);
        return new IntegrationException(message is null
            ? $"GitHub returned an error ({status})."
            : $"GitHub returned an error ({status}): {message}");
    }

    /// <summary>
    /// Reads the message from a GitHub error body, with the details of any validation errors.
    /// </summary>
    private static string? ErrorMessage(string body)
    {
        try
        {
            GitHubError? error = JsonSerializer.Deserialize<GitHubError>(body, JsonOptions);
            if (string.IsNullOrWhiteSpace(error?.Message))
            {
                return null;
            }

            string[] details = [.. error.ErrorMessages];
            return details.Length == 0 ? error.Message : $"{error.Message}. {string.Join(" ", details)}";
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// The next page from the <c>Link</c> header. It must be on the API host, so the token is only sent to GitHub.
    /// </summary>
    private Uri? NextPage(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Link", out IEnumerable<string>? values))
        {
            return null;
        }

        foreach (Match match in LinkPattern().Matches(string.Join(",", values)))
        {
            if (match.Groups["rel"].Value.Split(' ').Contains("next")
                && Uri.TryCreate(match.Groups["url"].Value, UriKind.Absolute, out Uri? next))
            {
                return Uri.Compare(next, _apiUrl, UriComponents.SchemeAndServer, UriFormat.Unescaped,
                    StringComparison.OrdinalIgnoreCase) == 0
                    ? next
                    : throw new IntegrationException($"GitHub linked to a page on another host: {next.Host}.");
            }
        }

        return null;
    }

    private static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out IEnumerable<string>? values) ? values.FirstOrDefault() : null;

    // One entry of a Link header, e.g. <https://api.github.com/user/repos?page=2>; rel="next"
    [GeneratedRegex("<(?<url>[^>]*)>[^,]*?;\\s*rel=\"(?<rel>[^\"]*)\"")]
    private static partial Regex LinkPattern();
}
