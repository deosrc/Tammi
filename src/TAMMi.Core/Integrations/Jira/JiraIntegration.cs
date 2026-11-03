using System.Globalization;
using System.Runtime.CompilerServices;
using TAMMi.Core.Credentials;

namespace TAMMi.Core.Integrations.Jira;

/// <summary>
/// Reads issues from a JIRA Cloud, Server or Data Center site, as tasks.
/// </summary>
/// <remarks>
/// <para>
/// The secret in <see cref="Credentials"/> is an API token for JIRA Cloud, used with
/// <see cref="JiraIntegrationOptions.Email"/>, or a personal access token for Server and Data Center.
/// </para>
/// <para>
/// Task keys are JIRA's issue keys, e.g. <c>ABC-123</c>. Descriptions are in JIRA wiki markup, as REST API
/// version 2 returns them.
/// </para>
/// </remarks>
public sealed class JiraIntegration : ITaskIntegration
{
    /// <summary>
    /// The search used when no query is given: the user's unfinished issues, most recently updated first.
    /// </summary>
    public const string DefaultJql = "assignee = currentUser() AND statusCategory != Done ORDER BY updated DESC";

    private readonly JiraIntegrationOptions _options;
    private readonly JiraApiClient _api;
    private readonly Uri _siteUrl;

    /// <param name="options">How to connect to the site.</param>
    /// <param name="credentialStore">Where the token is kept.</param>
    /// <param name="httpClient">Sends the requests. The caller owns it.</param>
    /// <param name="timeProvider">Times waits for rate limits to reset.</param>
    /// <exception cref="ArgumentException">
    /// The site URL isn't an HTTP or HTTPS URL, or a JIRA Cloud site has no email address.
    /// </exception>
    public JiraIntegration(
        JiraIntegrationOptions options,
        ICredentialStore credentialStore,
        HttpClient httpClient,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(credentialStore);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.PageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.PageSize, 100);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxSearchResults, 1);
        if (!options.SiteUrl.IsAbsoluteUri || options.SiteUrl.Scheme is not ("https" or "http"))
        {
            throw new ArgumentException("The site URL must be an absolute HTTP or HTTPS URL.", nameof(options));
        }

        if (options.Deployment == JiraDeployment.Cloud && string.IsNullOrWhiteSpace(options.Email))
        {
            throw new ArgumentException("JIRA Cloud needs the account's email address.", nameof(options));
        }

        _options = options;
        _siteUrl = JiraApiClient.SiteUrl(options.SiteUrl);
        Credentials = new IntegrationCredentials(credentialStore, options.Id);
        _api = new JiraApiClient(options, Credentials, httpClient, timeProvider);
    }

    /// <inheritdoc/>
    public string Id => _options.Id;

    /// <inheritdoc/>
    public string Name => _options.Name;

    /// <summary>
    /// The API token or personal access token. Save one to authenticate the integration.
    /// </summary>
    public IntegrationCredentials Credentials { get; }

    /// <summary>
    /// Reads the user the credentials belong to, e.g. to check them before saving.
    /// </summary>
    /// <exception cref="IntegrationAuthenticationException">The token is missing or was rejected.</exception>
    /// <exception cref="IntegrationException">JIRA couldn't be reached or returned an error.</exception>
    public async Task<JiraUser> GetAuthenticatedUserAsync(CancellationToken cancellationToken = default) =>
        await _api.GetAsync<JiraUser>("myself", allowNotFound: false, cancellationToken).ConfigureAwait(false)
            ?? throw new IntegrationException("JIRA didn't return the authenticated user.");

    /// <summary>
    /// Searches issues with JQL, e.g. <c>project = ABC AND status = "In Progress"</c>. With no query, it lists the
    /// user's unfinished issues (<see cref="DefaultJql"/>). An issue key (<c>ABC-123</c>) or the URL of an issue on
    /// the site finds that issue.
    /// </summary>
    /// <returns>Up to <see cref="JiraIntegrationOptions.MaxSearchResults"/> issues.</returns>
    /// <inheritdoc/>
    public async Task<IReadOnlyList<TaskInfo>> SearchTasksAsync(
        string? query,
        CancellationToken cancellationToken = default)
    {
        if (JiraIssueKey.TryParse(query, out string? key) || JiraIssueKey.TryParseUrl(query, _siteUrl, out key))
        {
            return await GetIssueAsync(key, cancellationToken).ConfigureAwait(false) is { } task ? [task] : [];
        }

        string jql = string.IsNullOrWhiteSpace(query) ? DefaultJql : query.Trim();
        var tasks = new List<TaskInfo>();
        await foreach (JiraIssue issue in SearchAsync(jql, cancellationToken).ConfigureAwait(false))
        {
            tasks.Add(ToTask(issue));
            if (tasks.Count >= _options.MaxSearchResults)
            {
                break;
            }
        }

        return tasks;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException"><paramref name="key"/> isn't an issue key such as <c>ABC-123</c>.</exception>
    public Task<TaskInfo?> GetTaskAsync(string key, CancellationToken cancellationToken = default) =>
        JiraIssueKey.TryParse(key, out string? issueKey)
            ? GetIssueAsync(issueKey, cancellationToken)
            : throw new ArgumentException($"'{key}' isn't a JIRA issue key such as 'ABC-123'.", nameof(key));

    private async Task<TaskInfo?> GetIssueAsync(string key, CancellationToken cancellationToken)
    {
        JiraIssue? issue = await _api.GetAsync<JiraIssue>(
            $"issue/{Uri.EscapeDataString(key)}?fields={JiraIssueFields.Fields}",
            allowNotFound: true,
            cancellationToken).ConfigureAwait(false);
        return issue is null ? null : ToTask(issue);
    }

    /// <summary>
    /// Reads the results of a search, page by page. JIRA Cloud pages with a token (<c>search/jql</c>); Server and
    /// Data Center page by position (<c>search</c>), which JIRA Cloud no longer offers.
    /// </summary>
    private async IAsyncEnumerable<JiraIssue> SearchAsync(
        string jql,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string maxResults = Math.Min(_options.PageSize, _options.MaxSearchResults)
            .ToString(CultureInfo.InvariantCulture);
        string query = $"jql={Uri.EscapeDataString(jql)}&fields={JiraIssueFields.Fields}&maxResults={maxResults}";

        if (_options.Deployment == JiraDeployment.Cloud)
        {
            string? pageToken = null;
            do
            {
                string path = pageToken is null
                    ? $"search/jql?{query}"
                    : $"search/jql?{query}&nextPageToken={Uri.EscapeDataString(pageToken)}";
                JiraCloudSearchResults page = (await _api
                    .GetAsync<JiraCloudSearchResults>(path, allowNotFound: false, cancellationToken)
                    .ConfigureAwait(false))!;
                foreach (JiraIssue issue in page.Issues ?? [])
                {
                    yield return issue;
                }

                // A token that doesn't change would ask for the same page forever.
                pageToken = page.IsLast == true || page.NextPageToken == pageToken ? null : page.NextPageToken;
            }
            while (!string.IsNullOrEmpty(pageToken));
        }
        else
        {
            int startAt = 0;
            while (true)
            {
                string path = $"search?{query}&startAt={startAt.ToString(CultureInfo.InvariantCulture)}";
                JiraDataCenterSearchResults page = (await _api
                    .GetAsync<JiraDataCenterSearchResults>(path, allowNotFound: false, cancellationToken)
                    .ConfigureAwait(false))!;
                IReadOnlyList<JiraIssue> issues = page.Issues ?? [];
                foreach (JiraIssue issue in issues)
                {
                    yield return issue;
                }

                startAt = page.StartAt + issues.Count;
                if (issues.Count == 0 || startAt >= page.Total)
                {
                    break;
                }
            }
        }
    }

    private TaskInfo ToTask(JiraIssue issue)
    {
        string? summary = issue.Fields?.Summary;
        string? description = issue.Fields?.Description;
        return new TaskInfo(
            Id,
            issue.Key,
            string.IsNullOrWhiteSpace(summary) ? issue.Key : summary,
            new Uri(_siteUrl, $"browse/{Uri.EscapeDataString(issue.Key)}"))
        {
            Description = string.IsNullOrWhiteSpace(description) ? null : description,
        };
    }
}
