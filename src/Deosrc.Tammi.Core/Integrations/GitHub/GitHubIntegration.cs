using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using Deosrc.Tammi.Core.Credentials;

namespace Deosrc.Tammi.Core.Integrations.GitHub;

/// <summary>
/// Reads issues (as tasks) and repositories from GitHub, or from a GitHub Enterprise Server.
/// </summary>
/// <remarks>
/// <para>
/// It authenticates with an access token kept in the credential store through <see cref="Credentials"/>: a
/// fine-grained or classic personal access token. Fine-grained tokens need read access to issues, contents and
/// metadata on the repositories to include: without contents, GitHub's issue search leaves out private repositories'
/// issues. Classic tokens need the <c>repo</c> scope for private repositories.
/// </para>
/// <para>
/// Task keys have the form <c>owner/repo#number</c>. Pull requests aren't tasks, so they're left out. Text searches
/// cover only the user's own, their organisations' and their collaborations' repositories; any other issue can be
/// found by its reference or URL.
/// </para>
/// </remarks>
public sealed class GitHubIntegration : ITaskIntegration, IRepositoryIntegration
{
    /// <summary>
    /// The search for the tasks assigned to the user: their open issues.
    /// </summary>
    public const string AssignedSearch = "is:open assignee:@me archived:false";

    private readonly GitHubIntegrationOptions _options;
    private readonly GitHubApiClient _api;
    private readonly string _webHost;
    private readonly TimeProvider _timeProvider;
    private readonly Lock _scopeLock = new();
    private IReadOnlyList<string>? _scope;
    private DateTimeOffset _scopeExpires;

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
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxScopeQualifiers, 1);
        if (!options.ApiUrl.IsAbsoluteUri || options.ApiUrl.Scheme is not ("https" or "http"))
        {
            throw new ArgumentException("The API URL must be an absolute HTTP or HTTPS URL.", nameof(options));
        }

        _options = options;
        _timeProvider = timeProvider;
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
    /// What fine-grained personal access tokens start with. Classic tokens start with <c>ghp_</c>.
    /// </summary>
    public const string FineGrainedTokenPrefix = "github_pat_";

    /// <summary>
    /// The most empty private repositories <see cref="CheckTokenAccessAsync"/> looks past to find one with content.
    /// </summary>
    private const int MaxEmptyRepositoriesChecked = 5;

    /// <summary>
    /// Checks that a fine-grained token can read the contents of the private repositories it reaches. Without that,
    /// GitHub's issue search leaves out their issues, though everything else works.
    /// </summary>
    /// <remarks>
    /// GitHub has no way to list a fine-grained token's permissions, so this reads the latest commit of one private
    /// repository the token reaches, which needs read access to contents. Repositories the token wasn't given at all
    /// can't be detected. Classic tokens aren't checked.
    /// </remarks>
    /// <returns>A warning to show the user, or <see langword="null"/> if there's nothing to warn about.</returns>
    /// <exception cref="IntegrationAuthenticationException">The token is missing or was rejected.</exception>
    /// <exception cref="IntegrationException">GitHub couldn't be reached or returned an error.</exception>
    public async Task<string?> CheckTokenAccessAsync(CancellationToken cancellationToken = default)
    {
        string token = await Credentials.GetRequiredAsync(cancellationToken).ConfigureAwait(false);
        if (!token.StartsWith(FineGrainedTokenPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        int empty = 0;
        string path = $"user/repos?visibility=private&per_page={MaxEmptyRepositoriesChecked + 1}";
        await foreach (GitHubRepository repository in _api
            .GetPagesAsync<IReadOnlyList<GitHubRepository>, GitHubRepository>(path, page => page, cancellationToken)
            .ConfigureAwait(false))
        {
            if (!repository.Private)
            {
                continue;
            }

            HttpStatusCode status = await _api.GetStatusAsync(
                $"repos/{Uri.EscapeDataString(repository.Owner.Login)}/{Uri.EscapeDataString(repository.Name)}"
                + "/commits?per_page=1",
                cancellationToken).ConfigureAwait(false);
            switch (status)
            {
                case HttpStatusCode.Conflict when ++empty < MaxEmptyRepositoriesChecked:
                    // An empty repository has no commits to read, so it can't tell. Try another.
                    continue;
                case HttpStatusCode.Forbidden or HttpStatusCode.NotFound:
                    return $"This token can't read the contents of private repositories such as "
                        + $"{repository.Owner.Login}/{repository.Name}, so their issues won't appear when you search. "
                        + "Give the token read access to Contents, then authenticate again.";
                default:
                    return null;
            }
        }

        return null;
    }

    /// <summary>
    /// Lists the user's open issues (<see cref="AssignedSearch"/>), most recently updated first. GitHub has no
    /// in-progress state, so every open issue counts as still to do.
    /// </summary>
    /// <returns>Up to <see cref="GitHubIntegrationOptions.MaxSearchResults"/> issues.</returns>
    /// <inheritdoc/>
    public Task<IReadOnlyList<TaskInfo>> GetAssignedTasksAsync(CancellationToken cancellationToken = default) =>
        SearchIssuesAsync([$"is:issue {AssignedSearch}"], sortByUpdated: true, cancellationToken);

    /// <summary>
    /// Searches the text of issues in the repositories of the user and of the organisations they belong to, and in
    /// the other repositories they collaborate on. Results are in GitHub's best-match order.
    /// </summary>
    /// <remarks>
    /// The text can hold GitHub's search qualifiers too, e.g. <c>label:bug</c>. The accounts and repositories
    /// searched are read from GitHub and remembered for <see cref="GitHubIntegrationOptions.SearchScopeLifetime"/>.
    /// </remarks>
    /// <returns>Up to <see cref="GitHubIntegrationOptions.MaxSearchResults"/> issues.</returns>
    /// <inheritdoc/>
    public async Task<IReadOnlyList<TaskInfo>> SearchTasksAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        IReadOnlyList<string> scope = await GetSearchScopeAsync(cancellationToken).ConfigureAwait(false);

        // GitHub matches any of several user:, org: and repo: qualifiers, so each request covers a batch of them.
        IEnumerable<string> searches = scope
            .Chunk(_options.MaxScopeQualifiers)
            .Select(batch => $"is:issue archived:false {string.Join(' ', batch)} {text.Trim()}");
        return await SearchIssuesAsync([.. searches], sortByUpdated: false, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads an issue reference (<c>owner/repo#42</c>) or the URL of an issue's page on this GitHub server.
    /// </summary>
    /// <inheritdoc/>
    public bool TryParseTaskReference(string? text, [NotNullWhen(true)] out string? key)
    {
        key = GitHubIssueKey.TryParse(text, out GitHubIssueKey? issue)
            || GitHubIssueKey.TryParseUrl(text, _webHost, out issue)
                ? issue.ToString()
                : null;
        return key is not null;
    }

    /// <summary>
    /// Runs searches one after another until they've found <see cref="GitHubIntegrationOptions.MaxSearchResults"/>
    /// issues.
    /// </summary>
    private async Task<IReadOnlyList<TaskInfo>> SearchIssuesAsync(
        IReadOnlyList<string> searches,
        bool sortByUpdated,
        CancellationToken cancellationToken)
    {
        string sort = sortByUpdated ? "&sort=updated&order=desc" : "";
        string perPage = Math.Min(_options.PageSize, _options.MaxSearchResults).ToString(CultureInfo.InvariantCulture);
        var tasks = new List<TaskInfo>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string search in searches)
        {
            string path = $"search/issues?q={Uri.EscapeDataString(search)}{sort}&per_page={perPage}";
            await foreach (GitHubIssue issue in _api
                .GetPagesAsync<GitHubSearchResults, GitHubIssue>(path, results => results.Items, cancellationToken)
                .ConfigureAwait(false))
            {
                if (ToTask(issue) is { } task && seen.Add(task.Key))
                {
                    tasks.Add(task);
                }

                if (tasks.Count >= _options.MaxSearchResults)
                {
                    return tasks;
                }
            }
        }

        return tasks;
    }

    /// <summary>
    /// Returns the search qualifiers for the issues a text search covers: <c>user:</c> for the user, <c>org:</c> for
    /// each organisation that owns a repository they can reach, and <c>repo:</c> for each other user's repository
    /// they collaborate on (so that user's other repositories aren't searched).
    /// </summary>
    private async Task<IReadOnlyList<string>> GetSearchScopeAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        lock (_scopeLock)
        {
            if (_scope is { } cached && now < _scopeExpires)
            {
                return cached;
            }
        }

        GitHubUser user = await GetAuthenticatedUserAsync(cancellationToken).ConfigureAwait(false);
        var owners = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { user.Login };
        var scope = new List<string> { $"user:{user.Login}" };
        var repositories = new List<string>();

        // The user's own repositories, those they collaborate on, and those of the organisations they're in.
        string path = $"user/repos?per_page={_options.PageSize.ToString(CultureInfo.InvariantCulture)}";
        await foreach (GitHubRepository repository in _api
            .GetPagesAsync<IReadOnlyList<GitHubRepository>, GitHubRepository>(path, page => page, cancellationToken)
            .ConfigureAwait(false))
        {
            GitHubAccount owner = repository.Owner;
            if (owner.IsOrganization)
            {
                if (owners.Add(owner.Login))
                {
                    scope.Add($"org:{owner.Login}");
                }
            }
            else if (!owners.Contains(owner.Login))
            {
                repositories.Add($"repo:{owner.Login}/{repository.Name}");
            }
        }

        scope.AddRange(repositories);
        lock (_scopeLock)
        {
            _scope = scope;
            _scopeExpires = now + _options.SearchScopeLifetime;
        }

        return scope;
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

    /// <returns>The task, or <see langword="null"/> for a pull request or an issue Tammi can't read.</returns>
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
            Updated = issue.UpdatedAt,
            RepositoryUrl = GetRepositoryUrl(url),
            Status = StatusName(issue),
            StatusCategory = issue.State switch
            {
                "open" => TaskStatusCategory.ToDo,
                "closed" => TaskStatusCategory.Done,
                _ => null,
            },
        };
    }

    /// <summary>
    /// The repository's web page from an issue's, e.g. <c>https://github.com/acme/api</c> from
    /// <c>https://github.com/acme/api/issues/42</c>. It's on the same host as the issue, so it suits Enterprise Servers
    /// too.
    /// </summary>
    private static Uri? GetRepositoryUrl(Uri issueUrl)
    {
        string path = issueUrl.AbsolutePath.TrimEnd('/');
        int issues = path.LastIndexOf("/issues/", StringComparison.Ordinal);
        return issues > 0 ? new Uri(issueUrl, path[..issues]) : null;
    }

    /// <summary>
    /// Names an issue's status as GitHub shows it: open, or closed and why. GitHub has no in-progress state.
    /// </summary>
    private static string? StatusName(GitHubIssue issue) => (issue.State, issue.StateReason) switch
    {
        ("open", _) => "Open",
        ("closed", "not_planned") => "Not planned",
        ("closed", "duplicate") => "Duplicate",
        ("closed", _) => "Closed",
        _ => null,
    };
}
