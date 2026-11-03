using System.Globalization;
using TAMMi.Core.Credentials;

namespace TAMMi.Core.Integrations.GitHub;

/// <summary>
/// Reads issues (as tasks) and repositories from GitHub, or from a GitHub Enterprise Server.
/// </summary>
/// <remarks>
/// <para>
/// It authenticates with an access token kept in the credential store through <see cref="Credentials"/>: a
/// fine-grained or classic personal access token. Fine-grained tokens need read access to issues and metadata on the
/// repositories to include. Classic tokens need the <c>repo</c> scope for private repositories.
/// </para>
/// <para>
/// Task keys have the form <c>owner/repo#number</c>. Pull requests aren't tasks, so they're left out.
/// </para>
/// </remarks>
public sealed class GitHubIntegration : ITaskIntegration, IRepositoryIntegration
{
    /// <summary>
    /// The search used when no query is given: the user's open issues.
    /// </summary>
    public const string DefaultSearch = "is:open assignee:@me archived:false";

    private readonly GitHubIntegrationOptions _options;
    private readonly GitHubApiClient _api;
    private readonly string _webHost;

    /// <param name="options">How to connect to GitHub.</param>
    /// <param name="credentialStore">Where the access token is kept.</param>
    /// <param name="httpClient">Sends the requests. The caller owns it.</param>
    /// <param name="timeProvider">Times waits for rate limits to reset.</param>
    public GitHubIntegration(
        GitHubIntegrationOptions options,
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
        if (!options.ApiUrl.IsAbsoluteUri || options.ApiUrl.Scheme is not ("https" or "http"))
        {
            throw new ArgumentException("The API URL must be an absolute HTTP or HTTPS URL.", nameof(options));
        }

        _options = options;
        Credentials = new IntegrationCredentials(credentialStore, options.Id);
        _api = new GitHubApiClient(options, Credentials, httpClient, timeProvider);

        // github.com serves its API from api.github.com. GitHub Enterprise Server serves both from one host.
        _webHost = string.Equals(options.ApiUrl.Host, "api.github.com", StringComparison.OrdinalIgnoreCase)
            ? "github.com"
            : options.ApiUrl.Host;
    }

    /// <inheritdoc/>
    public string Id => _options.Id;

    /// <inheritdoc/>
    public string Name => _options.Name;

    /// <summary>
    /// The access token. Save one to authenticate the integration.
    /// </summary>
    public IntegrationCredentials Credentials { get; }

    /// <summary>
    /// Reads the user the access token belongs to, e.g. to check a token before saving it.
    /// </summary>
    /// <exception cref="IntegrationAuthenticationException">The token is missing or was rejected.</exception>
    /// <exception cref="IntegrationException">GitHub couldn't be reached or returned an error.</exception>
    public async Task<GitHubUser> GetAuthenticatedUserAsync(CancellationToken cancellationToken = default) =>
        await _api.GetAsync<GitHubUser>("user", cancellationToken).ConfigureAwait(false)
            ?? throw new IntegrationException("GitHub didn't return the authenticated user.");

    /// <summary>
    /// Searches issues. The query uses GitHub's search syntax, e.g. <c>repo:acme/api is:open label:bug</c>, and
    /// <c>is:issue</c> is added to it. With no query, it lists the user's open issues (<see cref="DefaultSearch"/>).
    /// A task key (<c>acme/api#42</c>) or an issue's URL finds that issue.
    /// </summary>
    /// <returns>Up to <see cref="GitHubIntegrationOptions.MaxSearchResults"/> issues.</returns>
    /// <inheritdoc/>
    public async Task<IReadOnlyList<TaskInfo>> SearchTasksAsync(
        string? query,
        CancellationToken cancellationToken = default)
    {
        if (GitHubIssueKey.TryParse(query, out GitHubIssueKey? key)
            || GitHubIssueKey.TryParseUrl(query, _webHost, out key))
        {
            return await GetTaskAsync(key, cancellationToken).ConfigureAwait(false) is { } task ? [task] : [];
        }

        bool isDefault = string.IsNullOrWhiteSpace(query);
        string search = isDefault ? $"is:issue {DefaultSearch}" : $"is:issue {query!.Trim()}";

        // The default list shows recently updated issues first. A search keeps GitHub's best-match order.
        string sort = isDefault ? "&sort=updated&order=desc" : "";
        string perPage = Math.Min(_options.PageSize, _options.MaxSearchResults).ToString(CultureInfo.InvariantCulture);
        string path = $"search/issues?q={Uri.EscapeDataString(search)}{sort}&per_page={perPage}";

        var tasks = new List<TaskInfo>();
        await foreach (GitHubIssue issue in _api
            .GetPagesAsync<GitHubSearchResults, GitHubIssue>(path, results => results.Items, cancellationToken)
            .ConfigureAwait(false))
        {
            if (ToTask(issue) is { } task)
            {
                tasks.Add(task);
            }

            if (tasks.Count >= _options.MaxSearchResults)
            {
                break;
            }
        }

        return tasks;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">
    /// <paramref name="key"/> isn't in the form <c>owner/repo#number</c>.
    /// </exception>
    public Task<TaskInfo?> GetTaskAsync(string key, CancellationToken cancellationToken = default) =>
        GitHubIssueKey.TryParse(key, out GitHubIssueKey? issueKey)
            ? GetTaskAsync(issueKey, cancellationToken)
            : throw new ArgumentException($"'{key}' isn't a GitHub task key such as 'owner/repo#42'.", nameof(key));

    /// <summary>
    /// Lists the repositories the user owns, collaborates on or can reach through an organisation.
    /// </summary>
    /// <inheritdoc/>
    public async Task<IReadOnlyList<IntegrationRepository>> GetRepositoriesAsync(
        CancellationToken cancellationToken = default)
    {
        var path = $"user/repos?sort=full_name&per_page={_options.PageSize.ToString(CultureInfo.InvariantCulture)}";
        var repositories = new List<IntegrationRepository>();
        await foreach (GitHubRepository repository in _api
            .GetPagesAsync<IReadOnlyList<GitHubRepository>, GitHubRepository>(path, page => page, cancellationToken)
            .ConfigureAwait(false))
        {
            repositories.Add(new IntegrationRepository(
                Id,
                repository.Owner.Login,
                repository.Name,
                [repository.CloneUrl, repository.SshUrl])
            {
                Url = Uri.TryCreate(repository.HtmlUrl, UriKind.Absolute, out Uri? url) ? url : null,
            });
        }

        return repositories;
    }

    private async Task<TaskInfo?> GetTaskAsync(GitHubIssueKey key, CancellationToken cancellationToken)
    {
        string path = string.Create(CultureInfo.InvariantCulture,
            $"repos/{Uri.EscapeDataString(key.Owner)}/{Uri.EscapeDataString(key.Repository)}/issues/{key.Number}");
        GitHubIssue? issue = await _api.GetAsync<GitHubIssue>(path, cancellationToken).ConfigureAwait(false);
        return issue is null ? null : ToTask(issue);
    }

    /// <returns>The task, or <see langword="null"/> for a pull request or an issue TAMMi can't read.</returns>
    private TaskInfo? ToTask(GitHubIssue issue)
    {
        if (issue.IsPullRequest
            || GitHubIssueKey.ParseRepositoryApiUrl(issue.RepositoryUrl) is not var (owner, repository)
            || !Uri.TryCreate(issue.HtmlUrl, UriKind.Absolute, out Uri? url))
        {
            return null;
        }

        var key = new GitHubIssueKey(owner, repository, issue.Number);
        return new TaskInfo(Id, key.ToString(), issue.Title, url)
        {
            Description = string.IsNullOrEmpty(issue.Body) ? null : issue.Body,
        };
    }
}
