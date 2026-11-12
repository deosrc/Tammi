using System.Globalization;
using Deosrc.Tammi.Core.Credentials;
using Deosrc.Tammi.Core.Git;
using Microsoft.Extensions.Logging;

namespace Deosrc.Tammi.Core.Integrations.Bitbucket;

/// <summary>
/// Reads repositories and Pull Requests from Bitbucket Cloud.
/// </summary>
/// <remarks>
/// <para>
/// The secret in <see cref="Credentials"/> is an Atlassian API token with scopes, used with
/// <see cref="BitbucketIntegrationOptions.Email"/> as basic authentication. It needs the scopes in
/// <see cref="RequiredScopes"/>. Bitbucket Data Center isn't supported yet.
/// </para>
/// </remarks>
public sealed class BitbucketIntegration : IRepositoryIntegration, IPullRequestIntegration
{
    /// <summary>
    /// The scopes an API token needs: to read the user it belongs to, their workspaces, repositories and Pull Requests.
    /// </summary>
    public const string RequiredScopes =
        "read:pullrequest:bitbucket, read:repository:bitbucket, read:user:bitbucket and read:workspace:bitbucket";

    /// <summary>
    /// The message for a token that can't read Pull Requests, naming the scope it needs.
    /// </summary>
    internal const string PullRequestPermissionMessage =
        "This token can't read pull requests. Give it the read:pullrequest:bitbucket scope, then authenticate again.";

    /// <summary>
    /// The most Pull Requests (and their statuses) Bitbucket returns in a page. A larger page size is refused with
    /// "Invalid pagelen", unlike the lists of workspaces and repositories, which allow 100.
    /// </summary>
    internal const int MaxPullRequestPageSize = 50;

    /// <summary>
    /// Adds the participants (reviewers and whether they approved) to the Pull Requests Bitbucket lists, which leaves
    /// them out by default. The <c>+</c> (escaped, so it isn't read as a space) adds to the default fields rather than
    /// replacing them.
    /// </summary>
    private const string ParticipantsField = "&fields=%2Bvalues.participants";

    private static readonly string[] PullRequestStates = ["OPEN", "MERGED", "DECLINED", "SUPERSEDED"];

    private readonly BitbucketIntegrationOptions _options;
    private readonly BitbucketApiClient _api;
    private readonly ILogger _logger;

    /// <param name="options">How to connect to Bitbucket.</param>
    /// <param name="credentialStore">Where the API token is kept.</param>
    /// <param name="httpClient">Sends the requests. The caller owns it.</param>
    /// <param name="logger">Logs the requests and searches.</param>
    /// <param name="timeProvider">Times waits for rate limits to reset.</param>
    public BitbucketIntegration(
        BitbucketIntegrationOptions options,
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
        if (!options.ApiUrl.IsAbsoluteUri || options.ApiUrl.Scheme is not ("https" or "http"))
        {
            throw new ArgumentException("The API URL must be an absolute HTTP or HTTPS URL.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.Email))
        {
            throw new ArgumentException("Bitbucket Cloud needs the Atlassian account's email address.",
                nameof(options));
        }

        _options = options;
        _logger = logger;
        Credentials = new IntegrationCredentials(credentialStore, options.Id);
        _api = new BitbucketApiClient(options, Credentials, httpClient, logger, timeProvider);
    }

    /// <inheritdoc/>
    public string Id => _options.Id;

    /// <inheritdoc/>
    public string Name => _options.Name;

    /// <summary>
    /// The API token. Save one to authenticate the integration.
    /// </summary>
    public IntegrationCredentials Credentials { get; }

    /// <summary>
    /// Reads the user the API token belongs to, e.g. to check a token before saving it.
    /// </summary>
    /// <returns>The user's display name.</returns>
    /// <exception cref="IntegrationAuthenticationException">The token is missing or was rejected.</exception>
    /// <exception cref="IntegrationException">Bitbucket couldn't be reached or returned an error.</exception>
    public async Task<string> GetAuthenticatedUserAsync(CancellationToken cancellationToken = default)
    {
        BitbucketModels.User user = await GetUserAsync(cancellationToken).ConfigureAwait(false);
        return user.DisplayName ?? user.Nickname ?? _options.Email;
    }

    private async Task<BitbucketModels.User> GetUserAsync(CancellationToken cancellationToken) =>
        await _api.GetAsync<BitbucketModels.User>("user", allowNotFound: false, cancellationToken)
            .ConfigureAwait(false)
        ?? throw new IntegrationException("Bitbucket didn't return the authenticated user.");

    /// <summary>
    /// Lists the repositories the user is a member of in the workspaces they can access.
    /// </summary>
    /// <inheritdoc/>
    public async Task<IReadOnlyList<IntegrationRepository>> GetRepositoriesAsync(
        CancellationToken cancellationToken = default)
    {
        var repositories = new List<IntegrationRepository>();
        foreach (BitbucketModels.Repository repository in await ReadRepositoriesAsync(cancellationToken)
            .ConfigureAwait(false))
        {
            if (SplitFullName(repository.FullName) is not var (workspace, slug))
            {
                continue;
            }

            repositories.Add(new IntegrationRepository(Id, workspace, slug, CloneUrls(repository, workspace, slug))
            {
                Url = new Uri($"https://{BitbucketIntegrationOptions.WebHost}/{workspace}/{slug}"),
            });
        }

        return repositories;
    }

    /// <summary>
    /// Reads the repositories the user is a member of (<c>role=member</c>, which includes those they own, administer
    /// and contribute to) in each workspace they can access. Bitbucket has no single list of them.
    /// </summary>
    private async Task<List<BitbucketModels.Repository>> ReadRepositoriesAsync(CancellationToken cancellationToken)
    {
        string perPage = _options.PageSize.ToString(CultureInfo.InvariantCulture);
        var workspaces = new List<string>();
        await foreach (BitbucketModels.WorkspaceAccess access in _api
            .GetPagesAsync<BitbucketModels.WorkspaceAccess>(
                $"user/workspaces?pagelen={perPage}", allowNotFound: false, cancellationToken)
            .ConfigureAwait(false))
        {
            if (access.WorkspaceSlug is { Length: > 0 } slug)
            {
                workspaces.Add(slug);
            }
        }

        var repositories = new List<BitbucketModels.Repository>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string workspace in workspaces)
        {
            string path = $"repositories/{Uri.EscapeDataString(workspace)}?role=member&pagelen={perPage}";
            await foreach (BitbucketModels.Repository repository in _api
                .GetPagesAsync<BitbucketModels.Repository>(path, allowNotFound: true, cancellationToken)
                .ConfigureAwait(false))
            {
                if (repository.FullName is { } fullName && seen.Add(fullName))
                {
                    repositories.Add(repository);
                }
            }
        }

        return repositories;
    }

    /// <summary>
    /// The HTTPS and SSH URLs a repository is cloned from. Bitbucket's HTTPS URL names the user
    /// (<c>https://user@bitbucket.org/…</c>), which is left out so it matches other forms of the same URL.
    /// </summary>
    private static string[] CloneUrls(BitbucketModels.Repository repository, string workspace, string slug)
    {
        string? https = repository.Links?.CloneLinks?.FirstOrDefault(link => link.Name == "https")?.Href;
        string? ssh = repository.Links?.CloneLinks?.FirstOrDefault(link => link.Name == "ssh")?.Href;
        if (Uri.TryCreate(https, UriKind.Absolute, out Uri? httpsUrl))
        {
            https = new UriBuilder(httpsUrl) { UserName = "", Password = "" }.Uri.AbsoluteUri;
        }
        else
        {
            https = $"https://{BitbucketIntegrationOptions.WebHost}/{workspace}/{slug}.git";
        }

        return [https, ssh ?? $"git@{BitbucketIntegrationOptions.WebHost}:{workspace}/{slug}.git"];
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// It reads them from each repository on Bitbucket that a remote points at, by their source branch. Each Pull
    /// Request's checks (its commit statuses, e.g. builds) are read too where the token can, and are left out
    /// otherwise. Its review is <see cref="PullRequestReview.ChangesRequested"/> when any participant asked for
    /// changes, <see cref="PullRequestReview.Approved"/> when every reviewer approved, and
    /// <see cref="PullRequestReview.ReviewRequired"/> when it has reviewers who haven't.
    /// </para>
    /// <para>
    /// A repository that doesn't exist, or that the token can't see, has no Pull Requests. But when none of the
    /// repositories can be found, the token most likely wasn't given them, so that's an
    /// <see cref="IntegrationPermissionException"/>.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<PullRequestInfo>> GetPullRequestsAsync(
        IReadOnlyList<string> remoteUrls,
        string branch,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(remoteUrls);
        ArgumentException.ThrowIfNullOrWhiteSpace(branch);

        var pullRequests = new List<PullRequestInfo>();
        List<(string Workspace, string Repository)> repositories = RepositoriesOnBitbucket(remoteUrls);
        int found = 0;
        string states = string.Join('&', PullRequestStates.Select(state => $"state={state}"));
        string query = Uri.EscapeDataString($"source.branch.name = {Quote(branch)}");
        string perPage = Math.Min(_options.PageSize, MaxPullRequestPageSize).ToString(CultureInfo.InvariantCulture);
        foreach ((string workspace, string slug) in repositories)
        {
            string repositoryPath = $"repositories/{Uri.EscapeDataString(workspace)}/{Uri.EscapeDataString(slug)}";
            string path = $"{repositoryPath}/pullrequests?q={query}&{states}&sort=created_on&pagelen={perPage}"
                + ParticipantsField;
            var list = new List<BitbucketModels.PullRequest>();
            bool exists = false;
            try
            {
                await foreach (BitbucketModels.PullRequest pullRequest in _api
                    .GetPagesAsync<BitbucketModels.PullRequest>(path, allowNotFound: true, cancellationToken)
                    .ConfigureAwait(false))
                {
                    exists = true;
                    list.Add(pullRequest);
                }
            }
            catch (IntegrationPermissionException)
            {
                throw new IntegrationPermissionException(Id, PullRequestPermissionMessage);
            }

            // An empty list can't tell whether the repository exists, so check it when there's nothing to show.
            if (!exists && await _api.GetAsync<BitbucketModels.Repository>(
                repositoryPath, allowNotFound: true, cancellationToken).ConfigureAwait(false) is null)
            {
                continue;
            }

            found++;
            foreach (BitbucketModels.PullRequest pullRequest in list)
            {
                if (pullRequest.Source?.Repository?.FullName is { } sourceName
                    && remoteUrls.Any(url => GitRemoteUrls.IsSameRepository(
                        url, $"https://{BitbucketIntegrationOptions.WebHost}/{sourceName}"))
                    && await ToPullRequestAsync($"{workspace}/{slug}", repositoryPath, pullRequest, cancellationToken)
                        .ConfigureAwait(false) is { } info)
                {
                    pullRequests.Add(info);
                }
            }
        }

        if (repositories.Count > 0 && found == 0)
        {
            string names = string.Join(" or ", repositories.Select(repository => $"{repository.Workspace}/{repository.Repository}"));
            throw new IntegrationPermissionException(Id,
                $"Bitbucket can't find {names} with this token. Give the token access to the repository, then "
                + "authenticate again.");
        }

        _logger.LogDebug("Found {Count} pull requests from {Branch} at {Url}.", pullRequests.Count, branch,
            _options.ApiUrl);
        return pullRequests;
    }

    /// <summary>
    /// The workspace and name of each repository on Bitbucket Cloud that a remote URL points at, each once, in the
    /// order of the URLs.
    /// </summary>
    private static List<(string Workspace, string Repository)> RepositoriesOnBitbucket(IEnumerable<string> remoteUrls)
    {
        var repositories = new List<(string Workspace, string Repository)>();
        foreach (string url in remoteUrls)
        {
            if (GitRemoteUrls.TryGetHostAndPath(url, out string? host, out string? path)
                && string.Equals(host, BitbucketIntegrationOptions.WebHost, StringComparison.OrdinalIgnoreCase)
                && path.Split('/') is [{ Length: > 0 } workspace, { Length: > 0 } name]
                && !repositories.Any(repository =>
                    string.Equals(repository.Workspace, workspace, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(repository.Repository, name, StringComparison.OrdinalIgnoreCase)))
            {
                repositories.Add((workspace, name));
            }
        }

        return repositories;
    }

    /// <returns>The Pull Request, or <see langword="null"/> for one Tammi can't read.</returns>
    private async Task<PullRequestInfo?> ToPullRequestAsync(
        string repository,
        string repositoryPath,
        BitbucketModels.PullRequest pullRequest,
        CancellationToken cancellationToken)
    {
        PullRequestState? state = (pullRequest.State, pullRequest.Draft) switch
        {
            ("OPEN", true) => PullRequestState.Draft,
            ("OPEN", _) => PullRequestState.Open,
            ("MERGED", _) => PullRequestState.Merged,
            ("DECLINED" or "SUPERSEDED", _) => PullRequestState.Closed,
            _ => null,
        };
        if (state is null
            || !Uri.TryCreate(pullRequest.Links?.Html?.Href, UriKind.Absolute, out Uri? url))
        {
            return null;
        }

        return new PullRequestInfo(Id, repository, pullRequest.Id, pullRequest.Title ?? "", url, state.Value)
        {
            Review = ReviewOf(pullRequest.Participants),
            Checks = await GetChecksAsync(repositoryPath, pullRequest.Id, cancellationToken).ConfigureAwait(false),
        };
    }

    private static PullRequestReview? ReviewOf(IReadOnlyList<BitbucketModels.Participant>? participants)
    {
        if (participants?.Any(participant => participant.State == "changes_requested") == true)
        {
            return PullRequestReview.ChangesRequested;
        }

        List<BitbucketModels.Participant> reviewers =
            [.. (participants ?? []).Where(participant => participant.Role == "REVIEWER")];
        if (reviewers.Count == 0)
        {
            return null;
        }

        return reviewers.All(reviewer => reviewer.Approved == true || reviewer.State == "approved")
            ? PullRequestReview.Approved
            : PullRequestReview.ReviewRequired;
    }

    /// <returns>The combined result of the checks, or <see langword="null"/> when there are none or they can't be read.</returns>
    private async Task<PullRequestChecks?> GetChecksAsync(
        string repositoryPath,
        int number,
        CancellationToken cancellationToken)
    {
        string path = string.Create(CultureInfo.InvariantCulture,
            $"{repositoryPath}/pullrequests/{number}/statuses?pagelen={Math.Min(_options.PageSize, MaxPullRequestPageSize)}");
        var states = new List<string?>();
        try
        {
            await foreach (BitbucketModels.CommitStatus status in _api
                .GetPagesAsync<BitbucketModels.CommitStatus>(path, allowNotFound: true, cancellationToken)
                .ConfigureAwait(false))
            {
                states.Add(status.State);
            }
        }
        catch (IntegrationPermissionException)
        {
            // Reading the checks is a bonus: the Pull Request is still worth showing without them.
            return null;
        }

        if (states.Count == 0)
        {
            return null;
        }

        if (states.Any(state => state is "FAILED" or "STOPPED"))
        {
            return PullRequestChecks.Failed;
        }

        return states.All(state => state == "SUCCESSFUL") ? PullRequestChecks.Succeeded : PullRequestChecks.InProgress;
    }

    private static (string Workspace, string Repository)? SplitFullName(string? fullName) =>
        fullName?.Split('/') is [{ Length: > 0 } workspace, { Length: > 0 } repository]
            ? (workspace, repository)
            : null;

    /// <summary>
    /// A value in Bitbucket's query language: in double quotes, with quotes and backslashes escaped.
    /// </summary>
    private static string Quote(string value) =>
        "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)
        + "\"";
}
