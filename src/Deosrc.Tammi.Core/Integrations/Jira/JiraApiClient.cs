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
/// <remarks>
/// JIRA Cloud API tokens without scopes work at the site's own URL. Tokens with scopes only work through Atlassian's
/// API gateway (<see cref="GatewayUrl"/>), so when the site rejects a token, the request is tried there too, and the
/// one that worked is used first from then on.
/// </remarks>
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

    /// <summary>
    /// Atlassian's API gateway, which addresses a JIRA Cloud site by its cloud ID.
    /// </summary>
    internal static readonly Uri GatewayUrl = new("https://api.atlassian.com/ex/jira/");

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly Uri _siteApiUrl = new(SiteUrl(options.SiteUrl), "rest/api/2/");

    /// <summary>
    /// The site's API through the gateway, once its cloud ID is known.
    /// </summary>
    private Uri? _gatewayApiUrl;

    /// <summary>
    /// Whether the token last worked through the gateway rather than at the site.
    /// </summary>
    private volatile bool _preferGateway;

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
        using HttpResponseMessage? response = await SendAsync(path, allowNotFound, cancellationToken)
            .ConfigureAwait(false);
        return response is null ? null : await ReadAsync<T>(response, cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage?> SendAsync(
        string path,
        bool allowNotFound,
        CancellationToken cancellationToken)
    {
        AuthenticationHeaderValue authorization = Authorization(
            await credentials.GetRequiredAsync(cancellationToken).ConfigureAwait(false));

        bool viaGateway = _preferGateway && _gatewayApiUrl is not null;
        HttpResponseMessage? response = await SendAsync(
            new Uri(viaGateway ? _gatewayApiUrl! : _siteApiUrl, path), authorization, allowNotFound,
            cancellationToken).ConfigureAwait(false);
        if (response?.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        string body = await ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);
        Uri? otherApiUrl = options.Deployment != JiraDeployment.Cloud ? null
            : viaGateway ? _siteApiUrl
            : await GatewayApiUrlAsync(cancellationToken).ConfigureAwait(false);
        if (otherApiUrl is null)
        {
            throw new IntegrationAuthenticationException(options.Id, RejectedMessage(viaGateway ? body : null));
        }

        response = await SendAsync(new Uri(otherApiUrl, path), authorization, allowNotFound, cancellationToken)
            .ConfigureAwait(false);
        if (response?.StatusCode != HttpStatusCode.Unauthorized)
        {
            _preferGateway = !viaGateway;
            return response;
        }

        string otherBody = await ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);
        throw new IntegrationAuthenticationException(options.Id, RejectedMessage(viaGateway ? body : otherBody));
    }

    /// <summary>
    /// Sends a request, waiting out short rate limits.
    /// </summary>
    /// <returns>
    /// The response if it succeeded or the credentials were rejected (status 401), which the caller disposes, or
    /// <see langword="null"/> if it was allowed not to be found.
    /// </returns>
    private async Task<HttpResponseMessage?> SendAsync(
        Uri url,
        AuthenticationHeaderValue authorization,
        bool allowNotFound,
        CancellationToken cancellationToken)
    {
        for (int attempt = 0; ; attempt++)
        {
            HttpResponseMessage response = await SendOnceAsync(url, authorization, cancellationToken)
                .ConfigureAwait(false);
            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Unauthorized)
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
                if (CaptchaError(response) is { } captchaError)
                {
                    throw new IntegrationAuthenticationException(options.Id, captchaError);
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

    private static async Task<string> ReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using (response)
        {
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The site's API through the gateway, or <see langword="null"/> if the site doesn't say its cloud ID.
    /// </summary>
    private async Task<Uri?> GatewayApiUrlAsync(CancellationToken cancellationToken)
    {
        if (_gatewayApiUrl is { } known)
        {
            return known;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                new Uri(SiteUrl(options.SiteUrl), "_edge/tenant_info"));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue(AppInfo.Name, AppInfo.Version));
            using HttpResponseMessage response = await httpClient
                .SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            JiraTenantInfo? tenant = await ReadAsync<JiraTenantInfo>(response, cancellationToken)
                .ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(tenant.CloudId))
            {
                return null;
            }

            return _gatewayApiUrl = new Uri(GatewayUrl, $"{Uri.EscapeDataString(tenant.CloudId)}/rest/api/2/");
        }
        catch (Exception exception) when (exception is HttpRequestException or IntegrationException
            || (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return null;
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
    /// Why JIRA rejected the credentials (status 401).
    /// </summary>
    /// <param name="gatewayBody">
    /// The gateway's response, which says when a token with scopes lacks one, or <see langword="null"/> if the request
    /// didn't go through the gateway.
    /// </param>
    private string RejectedMessage(string? gatewayBody)
    {
        const string AuthenticateAgain = "Authenticate again in Settings → Integrations.";
        if (options.Deployment != JiraDeployment.Cloud)
        {
            return "JIRA rejected the personal access token. The token may have expired or been revoked. "
                + AuthenticateAgain;
        }

        if (gatewayBody is not null
            && ErrorMessage(gatewayBody)?.Contains("scope", StringComparison.OrdinalIgnoreCase) == true)
        {
            return $"The API token doesn't have the scopes {AppInfo.Name} needs: {JiraIntegration.CloudScopes}. "
                + "Create a token with them, then authenticate again in Settings → Integrations.";
        }

        return "JIRA rejected the email address and API token. Check the email address is your Atlassian account's. "
            + "The token may have expired or been revoked, or, if it has scopes, lack "
            + $"{JiraIntegration.CloudScopes}. "
            + AuthenticateAgain;
    }

    /// <summary>
    /// Why JIRA refused a login, or <see langword="null"/> if the response isn't a refused login.
    /// </summary>
    /// <remarks>
    /// After several failed logins, Server and Data Center refuse further logins with a 403 until the user solves a
    /// CAPTCHA in a browser, and say so in <c>X-Authentication-Denied-Reason</c>.
    /// </remarks>
    private static string? CaptchaError(HttpResponseMessage response) =>
        Header(response, "X-Authentication-Denied-Reason")?.Contains("CAPTCHA",
            StringComparison.OrdinalIgnoreCase) == true
            ? "JIRA wants a CAPTCHA solved after failed logins. Log in to JIRA in a browser, then try again."
            : null;

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
