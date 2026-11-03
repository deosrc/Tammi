using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace TAMMi.Core.Integrations.Jira;

/// <summary>
/// Calls JIRA's REST API: authenticates each request and waits out short rate limits. Errors become
/// <see cref="IntegrationException"/>s.
/// </summary>
internal sealed class JiraApiClient(
    JiraIntegrationOptions options,
    IntegrationCredentials credentials,
    HttpClient httpClient,
    TimeProvider timeProvider)
{
    /// <summary>
    /// How often a request is retried after a rate limit, so a limit that keeps coming back can't retry forever.
    /// </summary>
    internal const int MaxRateLimitRetries = 3;

    /// <summary>
    /// How long to wait after a rate limit that doesn't say how long to wait.
    /// </summary>
    internal static readonly TimeSpan DefaultRateLimitWait = TimeSpan.FromMinutes(1);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly Uri _apiUrl = new(SiteUrl(options.SiteUrl), "rest/api/2/");

    /// <summary>
    /// The site's base URL, ending in <c>/</c> so relative URLs keep its path.
    /// </summary>
    public static Uri SiteUrl(Uri siteUrl) =>
        siteUrl.AbsoluteUri.EndsWith('/') ? siteUrl : new Uri(siteUrl.AbsoluteUri + "/");

    /// <summary>
    /// Reads one resource.
    /// </summary>
    /// <param name="path">The path relative to <c>rest/api/2/</c>, with any query string, e.g. <c>myself</c>.</param>
    /// <param name="allowNotFound">Whether to return <see langword="null"/> when JIRA says it doesn't exist.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="IntegrationException">The request failed.</exception>
    public async Task<T?> GetAsync<T>(string path, bool allowNotFound, CancellationToken cancellationToken)
        where T : class
    {
        using HttpResponseMessage? response = await SendAsync(new Uri(_apiUrl, path), allowNotFound, cancellationToken)
            .ConfigureAwait(false);
        return response is null ? null : await ReadAsync<T>(response, cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage?> SendAsync(
        Uri url,
        bool allowNotFound,
        CancellationToken cancellationToken)
    {
        AuthenticationHeaderValue authorization = Authorization(
            await credentials.GetRequiredAsync(cancellationToken).ConfigureAwait(false));
        for (int attempt = 0; ; attempt++)
        {
            HttpResponseMessage response = await SendOnceAsync(url, authorization, cancellationToken)
                .ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            using (response)
            {
                if (allowNotFound && response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }

                string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if (AuthenticationError(response) is { } authenticationError)
                {
                    throw new IntegrationAuthenticationException(options.Id, authenticationError);
                }

                if (RateLimitRetryAt(response) is not { } retryAt)
                {
                    throw Error(response, body);
                }

                TimeSpan wait = retryAt - timeProvider.GetUtcNow();
                if (attempt >= MaxRateLimitRetries || wait > options.MaxRateLimitWait)
                {
                    throw new IntegrationRateLimitException(
                        options.Id,
                        $"JIRA's rate limit was reached. Try again after {retryAt.ToLocalTime():t}.",
                        retryAt);
                }

                if (wait > TimeSpan.Zero)
                {
                    await Task.Delay(wait, timeProvider, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    /// <summary>
    /// JIRA Cloud takes the account's email address and an API token as basic authentication. Server and Data
    /// Center take a personal access token as a bearer token.
    /// </summary>
    private AuthenticationHeaderValue Authorization(string token) => options.Deployment switch
    {
        JiraDeployment.Cloud => new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.Email}:{token}"))),
        _ => new AuthenticationHeaderValue("Bearer", token),
    };

    private async Task<HttpResponseMessage> SendOnceAsync(
        Uri url,
        AuthenticationHeaderValue authorization,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = authorization;
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue(AppInfo.Name, AppInfo.Version));

        try
        {
            return await httpClient
                .SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            throw new IntegrationException($"JIRA couldn't be reached: {exception.Message}", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new IntegrationException("JIRA didn't respond in time.", exception);
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
            throw new IntegrationException($"JIRA returned a response TAMMi couldn't read: {exception.Message}",
                exception);
        }
    }

    /// <summary>
    /// Why JIRA didn't accept the credentials, or <see langword="null"/> if the response isn't about them.
    /// </summary>
    /// <remarks>
    /// After several failed logins, Server and Data Center refuse further logins with a 403 until the user solves a
    /// CAPTCHA in a browser, and say so in <c>X-Authentication-Denied-Reason</c>.
    /// </remarks>
    private string? AuthenticationError(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            string credentials = options.Deployment == JiraDeployment.Cloud
                ? "the email address and API token"
                : "the personal access token";
            return $"JIRA rejected {credentials}. The token may have expired or been revoked. "
                + "Authenticate again in Settings → Integrations.";
        }

        return Header(response, "X-Authentication-Denied-Reason")?.Contains("CAPTCHA",
            StringComparison.OrdinalIgnoreCase) == true
            ? "JIRA wants a CAPTCHA solved after failed logins. Log in to JIRA in a browser, then try again."
            : null;
    }

    /// <summary>
    /// When a rate-limited request can be tried again, or <see langword="null"/> if the response isn't a rate limit.
    /// </summary>
    /// <remarks>
    /// JIRA signals a rate limit with status 429. JIRA Cloud can also answer 503 with <c>Retry-After</c> when it's
    /// overloaded. It says when to retry in <c>Retry-After</c> (seconds), and sometimes in <c>X-RateLimit-Reset</c>
    /// (an ISO 8601 time).
    /// </remarks>
    private DateTimeOffset? RateLimitRetryAt(HttpResponseMessage response)
    {
        RetryConditionHeaderValue? retryAfter = response.Headers.RetryAfter;
        if (response.StatusCode != HttpStatusCode.TooManyRequests
            && !(response.StatusCode == HttpStatusCode.ServiceUnavailable && retryAfter is not null))
        {
            return null;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        if (retryAfter is not null)
        {
            return retryAfter.Delta is { } delta ? now + delta : retryAfter.Date ?? now;
        }

        return DateTimeOffset.TryParse(Header(response, "X-RateLimit-Reset"), CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal, out DateTimeOffset reset)
            ? reset
            : now + DefaultRateLimitWait;
    }

    private static IntegrationException Error(HttpResponseMessage response, string body)
    {
        var status = $"{(int)response.StatusCode} {response.ReasonPhrase}".TrimEnd();
        string? message = ErrorMessage(body);
        return new IntegrationException(message is null
            ? $"JIRA returned an error ({status})."
            : $"JIRA returned an error ({status}): {message}");
    }

    private static string? ErrorMessage(string body)
    {
        try
        {
            return JsonSerializer.Deserialize<JiraError>(body, JsonOptions)?.Text;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out IEnumerable<string>? values) ? values.FirstOrDefault() : null;
}
