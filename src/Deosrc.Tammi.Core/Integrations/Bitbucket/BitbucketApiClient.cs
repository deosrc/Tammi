using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Deosrc.Tammi.Core.Integrations.Bitbucket;

/// <summary>
/// Calls Bitbucket Cloud's REST API: authenticates each request, follows paging links and waits out short rate
/// limits. Errors become <see cref="IntegrationException"/>s.
/// </summary>
internal sealed class BitbucketApiClient(
    BitbucketIntegrationOptions options,
    IntegrationCredentials credentials,
    HttpClient httpClient,
    ILogger logger,
    TimeProvider timeProvider)
{
    /// <summary>
    /// How often a request is retried after a rate limit, so a limit that keeps coming back can't retry forever.
    /// </summary>
    internal const int MaxRateLimitRetries = 3;

    /// <summary>
    /// How long to wait after a rate limit that doesn't say how long to wait. Bitbucket limits requests by the hour,
    /// so this is usually longer than the wait Tammi accepts.
    /// </summary>
    internal static readonly TimeSpan DefaultRateLimitWait = TimeSpan.FromMinutes(1);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly Uri _apiUrl = options.ApiUrl.AbsoluteUri.EndsWith('/')
        ? options.ApiUrl
        : new Uri(options.ApiUrl.AbsoluteUri + "/");

    /// <summary>
    /// Reads one resource.
    /// </summary>
    /// <param name="path">The path relative to the API URL, with any query string, e.g. <c>user</c>.</param>
    /// <param name="allowNotFound">Whether to return <see langword="null"/> when Bitbucket says it doesn't exist.</param>
    /// <exception cref="IntegrationException">The request failed.</exception>
    public async Task<T?> GetAsync<T>(string path, bool allowNotFound, CancellationToken cancellationToken)
        where T : class
    {
        using HttpResponseMessage? response = await SendAsync(new Uri(_apiUrl, path), allowNotFound,
            cancellationToken).ConfigureAwait(false);
        return response is null ? null : await ReadAsync<T>(response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads every item of a list, following the links to the following pages.
    /// </summary>
    /// <param name="path">The path of the first page, relative to the API URL.</param>
    /// <param name="allowNotFound">Whether a list that doesn't exist counts as empty.</param>
    public async IAsyncEnumerable<T> GetPagesAsync<T>(
        string path,
        bool allowNotFound,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Uri? next = new(_apiUrl, path);
        while (next is not null)
        {
            BitbucketModels.Page<T> page;
            using (HttpResponseMessage? response = await SendAsync(next, allowNotFound, cancellationToken)
                .ConfigureAwait(false))
            {
                if (response is null)
                {
                    yield break;
                }

                page = await ReadAsync<BitbucketModels.Page<T>>(response, cancellationToken).ConfigureAwait(false);
            }

            foreach (T item in page.Values ?? [])
            {
                yield return item;
            }

            // The link is followed as given, but only on Bitbucket's own API, so the token isn't sent elsewhere.
            next = Uri.TryCreate(page.Next, UriKind.Absolute, out Uri? link)
                && string.Equals(link.Host, _apiUrl.Host, StringComparison.OrdinalIgnoreCase)
                    ? link
                    : null;
        }
    }

    /// <summary>
    /// Sends a JSON body, e.g. to add a resource.
    /// </summary>
    /// <param name="path">The path relative to the API URL.</param>
    /// <param name="body">What to send, as JSON.</param>
    /// <exception cref="IntegrationException">The request failed.</exception>
    public async Task PostAsync<TBody>(string path, TBody body, CancellationToken cancellationToken)
    {
        string json = JsonSerializer.Serialize(body, JsonOptions);
        using HttpResponseMessage? response = await SendAsync(
            new Uri(_apiUrl, path), allowNotFound: false, cancellationToken, HttpMethod.Post, json)
            .ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage?> SendAsync(
        Uri url,
        bool allowNotFound,
        CancellationToken cancellationToken,
        HttpMethod? method = null,
        string? json = null)
    {
        string token = await credentials.GetRequiredAsync(cancellationToken).ConfigureAwait(false);
        for (int attempt = 0; ; attempt++)
        {
            HttpResponseMessage response = await SendOnceAsync(url, token, method ?? HttpMethod.Get, json, cancellationToken).ConfigureAwait(false);
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
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new IntegrationAuthenticationException(
                        options.Id,
                        "Bitbucket rejected the email address and API token. Check the email address is your "
                        + "Atlassian account's. The token may have expired or been revoked. "
                        + "Authenticate again in Settings → Integrations.");
                }

                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    string reason = ErrorMessage(body) ?? "The token isn't allowed to read that.";
                    throw new IntegrationPermissionException(
                        options.Id,
                        $"Bitbucket refused the request: {reason} "
                        + $"Give the API token the scopes {BitbucketIntegration.RequiredScopes}, "
                        + "then authenticate again in Settings → Integrations.");
                }

                if (response.StatusCode != HttpStatusCode.TooManyRequests)
                {
                    throw Error(response, body);
                }

                DateTimeOffset retryAt = RetryAt(response);
                TimeSpan wait = retryAt - timeProvider.GetUtcNow();
                if (attempt >= MaxRateLimitRetries || wait > options.MaxRateLimitWait)
                {
                    throw new IntegrationRateLimitException(
                        options.Id,
                        $"Bitbucket's rate limit was reached. Try again after {retryAt.ToLocalTime():t}.",
                        retryAt);
                }

                if (wait > TimeSpan.Zero)
                {
                    logger.LogInformation(
                        "Bitbucket's rate limit was reached at {Url}. Retrying in {Wait}.", url, wait);
                    await Task.Delay(wait, timeProvider, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    private async Task<HttpResponseMessage> SendOnceAsync(
        Uri url,
        string token,
        HttpMethod method,
        string? json,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, url);
        if (json is not null)
        {
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.Email}:{token}")));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue(AppInfo.Name, AppInfo.Version));

        try
        {
            HttpResponseMessage response = await httpClient
                .SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken)
                .ConfigureAwait(false);
            logger.LogDebug("{Method} {Url} returned {StatusCode}.", method, url, (int)response.StatusCode);
            return response;
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Bitbucket couldn't be reached at {Url}.", url);
            throw new IntegrationException($"Bitbucket couldn't be reached: {exception.Message}", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Bitbucket didn't respond in time at {Url}.", url);
            throw new IntegrationException("Bitbucket didn't respond in time.", exception);
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
            throw new IntegrationException(
                $"Bitbucket returned a response Tammi couldn't read: {exception.Message}", exception);
        }
    }

    /// <summary>
    /// When a rate-limited request can be tried again: <c>Retry-After</c> (seconds or a date), or
    /// <see cref="DefaultRateLimitWait"/> from now.
    /// </summary>
    private DateTimeOffset RetryAt(HttpResponseMessage response)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        RetryConditionHeaderValue? retryAfter = response.Headers.RetryAfter;
        if (retryAfter is null)
        {
            return now + DefaultRateLimitWait;
        }

        return retryAfter.Delta is { } delta ? now + delta : retryAfter.Date ?? now;
    }

    private static IntegrationException Error(HttpResponseMessage response, string body)
    {
        var status = string.Create(CultureInfo.InvariantCulture,
            $"{(int)response.StatusCode} {response.ReasonPhrase}").TrimEnd();
        string? message = ErrorMessage(body);
        return new IntegrationException(message is null
            ? $"Bitbucket returned an error ({status})."
            : $"Bitbucket returned an error ({status}): {message}");
    }

    private static string? ErrorMessage(string body)
    {
        try
        {
            BitbucketModels.ErrorBody? error = JsonSerializer.Deserialize<BitbucketModels.Error>(body, JsonOptions)
                ?.Body;
            return string.Join(" ", new[] { error?.Message, error?.Detail is { ValueKind: JsonValueKind.String } detail ? detail.GetString() : null }
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Select(text => text!.Trim())) is { Length: > 0 } message
                    ? message
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
