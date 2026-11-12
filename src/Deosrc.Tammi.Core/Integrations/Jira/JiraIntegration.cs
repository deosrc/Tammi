using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Deosrc.Tammi.Core.Credentials;
using Microsoft.Extensions.Logging;

namespace Deosrc.Tammi.Core.Integrations.Jira;

/// <summary>
/// Reads issues from a JIRA Cloud, Server or Data Center site, as tasks.
/// </summary>
/// <remarks>
/// <para>
/// The secret in <see cref="Credentials"/> is an API token for JIRA Cloud, used with
/// <see cref="JiraIntegrationOptions.Email"/>, or a personal access token for Server and Data Center. A JIRA Cloud
/// token can be one with scopes (<see cref="CloudScopes"/>), which is used through Atlassian's API gateway.
/// </para>
/// <para>
/// Task keys are JIRA's issue keys, e.g. <c>ABC-123</c>. Descriptions are in JIRA wiki markup, as REST API
/// version 2 returns them.
/// </para>
/// </remarks>
public sealed partial class JiraIntegration : ITaskIntegration
{
    /// <summary>
    /// The search for the tasks assigned to the user: their issues that are still to do (neither in progress nor
    /// done), most recently updated first.
    /// </summary>
    public const string AssignedJql = "assignee = currentUser() AND statusCategory = \"To Do\" ORDER BY updated DESC";

    /// <summary>
    /// The scopes a JIRA Cloud API token with scopes needs: to read issues, and to read the user it belongs to. A
    /// token without scopes can do whatever the user can.
    /// </summary>
    public const string CloudScopes = "read:jira-user and read:jira-work";

    private readonly JiraIntegrationOptions _options;
    private readonly JiraApiClient _api;
    private readonly Uri _siteUrl;
    private readonly ILogger _logger;

    /// <param name="options">How to connect to the site.</param>
    /// <param name="credentialStore">Where the token is kept.</param>
    /// <param name="httpClient">Sends the requests. The caller owns it.</param>
    /// <param name="logger">Logs the requests and searches.</param>
    /// <param name="timeProvider">Times waits for rate limits to reset.</param>
    /// <exception cref="ArgumentException">
    /// The site URL isn't an HTTP or HTTPS URL, or a JIRA Cloud site has no email address.
    /// </exception>
    public JiraIntegration(
        JiraIntegrationOptions options,
        ICredentialStore credentialStore,
        HttpClient httpClient,
        ILogger logger,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(credentialStore);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(logger);
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
        _logger = logger;
        _siteUrl = JiraApiClient.SiteUrl(options.SiteUrl);
        Credentials = new IntegrationCredentials(credentialStore, options.Id);
        _api = new JiraApiClient(options, Credentials, httpClient, logger, timeProvider);
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
    /// Lists the user's issues that are still to do (<see cref="AssignedJql"/>).
    /// </summary>
    /// <returns>Up to <see cref="JiraIntegrationOptions.MaxSearchResults"/> issues.</returns>
    /// <inheritdoc/>
    public Task<IReadOnlyList<TaskInfo>> GetAssignedTasksAsync(CancellationToken cancellationToken = default) =>
        SearchJqlAsync(AssignedJql, cancellationToken);

    /// <summary>
    /// Searches the text of the site's issues that the user can see (<see cref="TextJql"/>), most recently updated
    /// first.
    /// </summary>
    /// <returns>Up to <see cref="JiraIntegrationOptions.MaxSearchResults"/> issues.</returns>
    /// <inheritdoc/>
    public Task<IReadOnlyList<TaskInfo>> SearchTasksAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        return SearchJqlAsync(TextJql(text), cancellationToken);
    }

    /// <summary>
    /// Reads an issue key (<c>ABC-123</c>) or the URL of an issue on the site: its page, or a board or search with
    /// it selected.
    /// </summary>
    /// <inheritdoc/>
    public bool TryParseTaskReference(string? text, [NotNullWhen(true)] out string? key) =>
        JiraIssueKey.TryParse(text, out key) || JiraIssueKey.TryParseUrl(text, _siteUrl, out key);

    /// <summary>
    /// The most projects a bare issue number is tried in.
    /// </summary>
    private const int MaxShorthandProjects = 25;

    /// <summary>
    /// Finds the issues a bare number (<c>1234</c>) could mean, by trying it with each of the site's project keys.
    /// Each connected JIRA integration does this, so a number can find an issue on every site.
    /// </summary>
    /// <inheritdoc/>
    public async Task<IReadOnlyList<TaskInfo>> FindByShorthandAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!IssueNumberPattern().IsMatch(text.Trim()))
        {
            return [];
        }

        string number = text.Trim();
        IReadOnlyList<JiraProject> projects = await _api
            .GetAsync<IReadOnlyList<JiraProject>>("project", allowNotFound: false, cancellationToken)
            .ConfigureAwait(false) ?? [];
        TaskInfo?[] found = await Task.WhenAll(projects
            .Where(project => !string.IsNullOrWhiteSpace(project.Key))
            .Take(MaxShorthandProjects)
            .Select(project => GetIssueAsync($"{project.Key}-{number}", cancellationToken))).ConfigureAwait(false);
        return [.. found.OfType<TaskInfo>()];
    }

    [GeneratedRegex("^[1-9][0-9]{0,8}$")]
    private static partial Regex IssueNumberPattern();

    /// <summary>
    /// The JQL that searches issues' text (summary, description and comments) for words. Characters that JQL or its
    /// text search treat specially are left out, so any text the user types is a valid search.
    /// </summary>
    public static string TextJql(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var words = new System.Text.StringBuilder(text.Length);
        foreach (char c in text)
        {
            words.Append(char.IsControl(c) || "\"'+-&|!(){}[]^~*?:/\\".Contains(c) ? ' ' : c);
        }

        string phrase = string.Join(' ', words.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return $"text ~ \"{phrase}\" AND statusCategory != \"Done\" ORDER BY updated DESC";
    }

    private async Task<IReadOnlyList<TaskInfo>> SearchJqlAsync(string jql, CancellationToken cancellationToken)
    {
        var tasks = new List<TaskInfo>();
        await foreach (JiraIssue issue in SearchAsync(jql, cancellationToken).ConfigureAwait(false))
        {
            tasks.Add(ToTask(issue));
            if (tasks.Count >= _options.MaxSearchResults)
            {
                break;
            }
        }

        _logger.LogDebug("Found {Count} tasks for JQL '{Jql}' at {Url}.", tasks.Count, jql, _siteUrl);
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
            Updated = issue.Fields?.UpdatedTime,
            Status = string.IsNullOrWhiteSpace(issue.Fields?.Status?.Name) ? null : issue.Fields.Status.Name,
            StatusCategory = issue.Fields?.Status?.StatusCategory?.Category,
        };
    }
}
