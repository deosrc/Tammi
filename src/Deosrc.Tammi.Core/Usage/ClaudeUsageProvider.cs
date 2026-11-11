using System.Net.Http.Headers;
using System.Text.Json;
using Deosrc.Tammi.Core.Agents;

namespace Deosrc.Tammi.Core.Usage;

/// <summary>
/// Reads Claude Code's usage from Anthropic, using the sign-in Claude Code keeps in <c>~/.claude/.credentials.json</c>.
/// </summary>
/// <remarks>
/// This is the endpoint Claude Code's own <c>/usage</c> command uses. It isn't a documented public API, so it can
/// change. Subscription (OAuth) sign-ins only; API key users have no plan limits to show.
/// </remarks>
public sealed class ClaudeUsageProvider : IAgentUsageProvider
{
    private const string UsageUrl = "https://api.anthropic.com/api/oauth/usage";

    private static readonly (string Property, string Name, string Description)[] Limits =
    [
        ("five_hour", "5h", "5 hour session"),
        ("seven_day", "Weekly", "Weekly, all models"),
        ("seven_day_opus", "Weekly Opus", "Weekly, Opus"),
        ("seven_day_sonnet", "Weekly Sonnet", "Weekly, Sonnet"),
    ];

    private readonly HttpClient _http;
    private readonly string _credentialsPath;
    private readonly TimeProvider _time;

    public ClaudeUsageProvider(HttpClient http, string? credentialsPath = null, TimeProvider? time = null)
    {
        _http = http;
        _time = time ?? TimeProvider.System;
        _credentialsPath = credentialsPath ?? Path.Combine(
            Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR") is { Length: > 0 } configDir
                ? configDir
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude"),
            ".credentials.json");
    }

    /// <inheritdoc />
    public string AgentName => SupportedAgent.ClaudeCodeName;

    /// <inheritdoc />
    public async Task<AgentUsage> GetUsageAsync(CancellationToken cancellationToken = default)
    {
        var token = await ReadTokenAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, UsageUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("anthropic-beta", "oauth-2025-04-20");

        try
        {
            using var response = await _http.SendAsync(request, cancellationToken);
            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            {
                throw new AgentUsageException("Claude Code's sign-in has expired. Run Claude Code to sign in again.");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new AgentUsageException($"Anthropic returned {(int)response.StatusCode} when asked for usage.");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return Parse(json, _time.GetUtcNow());
        }
        catch (HttpRequestException ex)
        {
            throw new AgentUsageException($"Couldn't reach Anthropic. {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AgentUsageException("Anthropic took too long to respond.", ex);
        }
    }

    /// <summary>
    /// Reads the limits from the usage response. Limits the response doesn't have (or has as null) are left out.
    /// </summary>
    public static AgentUsage Parse(string json, DateTimeOffset fetchedAt)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var limits = new List<AgentUsageLimit>();
            foreach (var (property, name, description) in Limits)
            {
                if (!document.RootElement.TryGetProperty(property, out var element)
                    || element.ValueKind != JsonValueKind.Object
                    || !element.TryGetProperty("utilization", out var utilization)
                    || utilization.ValueKind != JsonValueKind.Number)
                {
                    continue;
                }

                DateTimeOffset? resetsAt = element.TryGetProperty("resets_at", out var resets)
                    && resets.ValueKind == JsonValueKind.String
                    && resets.TryGetDateTimeOffset(out var parsed)
                    ? parsed
                    : null;
                limits.Add(new AgentUsageLimit(name, description, Math.Clamp(utilization.GetDouble(), 0, 100), resetsAt));
            }

            return new AgentUsage(limits, fetchedAt);
        }
        catch (JsonException ex)
        {
            throw new AgentUsageException("Anthropic's usage response wasn't understood.", ex);
        }
    }

    private async Task<string> ReadTokenAsync(CancellationToken cancellationToken)
    {
        string text;
        try
        {
            text = await File.ReadAllTextAsync(_credentialsPath, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new AgentUsageException("Claude Code isn't signed in. Run Claude Code and sign in with your subscription.", ex);
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.TryGetProperty("claudeAiOauth", out var oauth)
                && oauth.TryGetProperty("accessToken", out var token)
                && token.ValueKind == JsonValueKind.String
                && !string.IsNullOrEmpty(token.GetString()))
            {
                return token.GetString()!;
            }
        }
        catch (JsonException)
        {
            // Reported below.
        }

        throw new AgentUsageException("Claude Code isn't signed in with a subscription, so it has no usage limits to show.");
    }
}
