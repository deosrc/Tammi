using Deosrc.Tammi.Core.Credentials;
using Deosrc.Tammi.Core.Integrations.GitHub;
using Deosrc.Tammi.Core.Integrations.Jira;
using Deosrc.Tammi.Core.Settings;
using Microsoft.Extensions.Logging;

namespace Deosrc.Tammi.Core.Integrations;

/// <summary>
/// Adds, authenticates, checks and removes the integrations configured in Settings. It keeps the
/// <see cref="Registry"/>, the <see cref="IntegrationsSettings"/> and the credential store in step.
/// </summary>
/// <remarks>
/// <para>
/// Credentials are checked with the service before they're saved, so a mistyped or rejected token is never stored and
/// an integration is never added without working credentials.
/// </para>
/// <para>
/// Safe to use from any thread. Adding, authenticating and removing run one at a time.
/// </para>
/// </remarks>
public sealed class IntegrationManager
{
    private readonly ISettingsStore _settingsStore;
    private readonly ICredentialStore _credentialStore;
    private readonly HttpClient _httpClient;
    private readonly ILoggerFactory _loggerFactory;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _changeLock = new(1, 1);
    private readonly Lock _statusLock = new();
    private readonly Dictionary<string, (long Check, IntegrationStatus Status)> _statuses = [];
    private long _lastCheck;

    /// <param name="settingsStore">Where the configured integrations are kept.</param>
    /// <param name="credentialStore">Where their secrets are kept.</param>
    /// <param name="httpClient">Sends the integrations' requests. The caller owns it.</param>
    /// <param name="timeProvider">Times waits for rate limits to reset.</param>
    public IntegrationManager(
        ISettingsStore settingsStore,
        ICredentialStore credentialStore,
        HttpClient httpClient,
        ILoggerFactory loggerFactory,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(credentialStore);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _settingsStore = settingsStore;
        _credentialStore = credentialStore;
        _httpClient = httpClient;
        _loggerFactory = loggerFactory;
        _timeProvider = timeProvider;

        IntegrationsSettings settings = settingsStore.Current.Integrations;
        Registry = new IntegrationRegistry([
            .. settings.GitHub.Select(github => Create(github, credentialStore)),
            .. settings.Jira.Select(jira => Create(jira, credentialStore)),
        ]);
    }

    /// <summary>
    /// The configured integrations. Use the manager, not the registry, to add or remove them.
    /// </summary>
    public IntegrationRegistry Registry { get; }

    /// <summary>
    /// Raised after an integration is added, authenticated or removed, or its status changes. It can be raised on any
    /// thread.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// The result of the integration's last connection check.
    /// </summary>
    public IntegrationStatus GetStatus(string id)
    {
        lock (_statusLock)
        {
            return _statuses.TryGetValue(id, out var entry) ? entry.Status : IntegrationStatus.NotChecked;
        }
    }

    /// <summary>
    /// Checks every integration's connection.
    /// </summary>
    public Task CheckAllAsync(CancellationToken cancellationToken = default) =>
        Task.WhenAll(Registry.All.Select(integration => CheckAsync(integration, cancellationToken)));

    /// <summary>
    /// Checks that the integration's service accepts its stored credentials, and records the result as its status.
    /// </summary>
    /// <returns>The new status.</returns>
    /// <exception cref="ArgumentException">There is no integration with that ID.</exception>
    public Task<IntegrationStatus> CheckAsync(string id, CancellationToken cancellationToken = default) =>
        CheckAsync(
            Registry.Find(id) ?? throw new ArgumentException($"There is no integration with ID '{id}'.", nameof(id)),
            cancellationToken);

    private async Task<IntegrationStatus> CheckAsync(IIntegration integration, CancellationToken cancellationToken)
    {
        string id = integration.Id;
        long check = StartCheck(id);
        IntegrationStatus status;
        try
        {
            status = await CredentialsOf(integration).GetAsync(cancellationToken).ConfigureAwait(false) is null
                ? new IntegrationStatus(IntegrationConnectionState.NotAuthenticated)
                : await GetConnectedStatusAsync(integration, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            SetStatus(id, IntegrationStatus.NotChecked, check);
            throw;
        }
        catch (Exception ex) when (ex is IntegrationException or CredentialStoreException)
        {
            status = Failure(ex);
        }

        SetStatus(id, status, check);
        return status;
    }

    /// <summary>
    /// Connects a GitHub integration: checks the token with GitHub, then saves it and the integration.
    /// </summary>
    /// <exception cref="ArgumentException">The settings aren't valid.</exception>
    /// <exception cref="IntegrationAuthenticationException">GitHub rejected the token.</exception>
    /// <exception cref="IntegrationException">
    /// The integration is already connected, or GitHub couldn't be reached or returned an error.
    /// </exception>
    /// <exception cref="CredentialStoreException">The token couldn't be saved.</exception>
    /// <exception cref="IOException">The settings couldn't be saved.</exception>
    public Task<IIntegration> AddGitHubAsync(
        GitHubIntegrationSettings settings,
        string token,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.IsValid())
        {
            throw new ArgumentException("The GitHub settings need an ID and an HTTP or HTTPS API URL.",
                nameof(settings));
        }

        return AddAsync(
            settings.Id,
            settings.Name,
            token,
            store => Create(settings, store),
            integrations => integrations with { GitHub = [.. integrations.GitHub, settings] },
            cancellationToken);
    }

    /// <summary>
    /// Connects a JIRA site: checks the token with JIRA, then saves it and the integration.
    /// </summary>
    /// <exception cref="ArgumentException">The settings aren't valid, e.g. a JIRA Cloud site has no email address.</exception>
    /// <exception cref="IntegrationAuthenticationException">JIRA rejected the token.</exception>
    /// <exception cref="IntegrationException">
    /// The site is already connected, or JIRA couldn't be reached or returned an error.
    /// </exception>
    /// <exception cref="CredentialStoreException">The token couldn't be saved.</exception>
    /// <exception cref="IOException">The settings couldn't be saved.</exception>
    public Task<IIntegration> AddJiraAsync(
        JiraIntegrationSettings settings,
        string token,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.IsValid())
        {
            throw new ArgumentException(
                "The JIRA settings need an ID and an HTTP or HTTPS site URL, and JIRA Cloud needs an email address.",
                nameof(settings));
        }

        return AddAsync(
            settings.Id,
            settings.Name,
            token,
            store => Create(settings, store),
            integrations => integrations with { Jira = [.. integrations.Jira, settings] },
            cancellationToken);
    }

    /// <summary>
    /// Replaces an integration's credentials: checks them with the service, then saves them.
    /// </summary>
    /// <param name="id">The integration's ID.</param>
    /// <param name="secret">The new token.</param>
    /// <param name="email">
    /// For a JIRA Cloud site, the account's email address to use with the token, or <see langword="null"/> to keep
    /// the current one. Other integrations ignore it.
    /// </param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <exception cref="ArgumentException">There is no integration with that ID.</exception>
    /// <exception cref="IntegrationAuthenticationException">The service rejected the credentials.</exception>
    /// <exception cref="IntegrationException">The service couldn't be reached or returned an error.</exception>
    /// <exception cref="CredentialStoreException">The token couldn't be saved.</exception>
    /// <exception cref="IOException">The settings couldn't be saved.</exception>
    public async Task AuthenticateAsync(
        string id,
        string secret,
        string? email = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        secret = secret.Trim();

        await _changeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            IntegrationsSettings integrations = _settingsStore.Current.Integrations;
            Func<ICredentialStore, IIntegration> create;
            JiraIntegrationSettings? changedJira = null;
            if (integrations.GitHub.FirstOrDefault(github => github.Id == id) is { } github)
            {
                create = store => Create(github, store);
            }
            else if (integrations.Jira.FirstOrDefault(jira => jira.Id == id) is { } jira)
            {
                if (jira.Deployment == JiraDeployment.Cloud
                    && !string.IsNullOrWhiteSpace(email)
                    && email.Trim() != jira.Email)
                {
                    jira = changedJira = jira with { Email = email.Trim() };
                }

                create = store => Create(jira, store);
            }
            else
            {
                throw new ArgumentException($"There is no integration with ID '{id}'.", nameof(id));
            }

            IntegrationStatus connected = await GetConnectedStatusAsync(
                create(new CandidateCredentialStore(secret)), cancellationToken).ConfigureAwait(false);

            await new IntegrationCredentials(_credentialStore, id).SaveAsync(secret, cancellationToken)
                .ConfigureAwait(false);

            if (changedJira is not null)
            {
                await _settingsStore.UpdateAsync(
                    settings => settings with
                    {
                        Integrations = settings.Integrations with
                        {
                            Jira = [.. settings.Integrations.Jira.Select(jira => jira.Id == id ? changedJira : jira)],
                        },
                    },
                    CancellationToken.None).ConfigureAwait(false);

                // The options are fixed when an integration is created, so replace it.
                Registry.Remove(id);
                Registry.Add(create(_credentialStore));
            }

            SetStatus(id, connected);
        }
        finally
        {
            _changeLock.Release();
        }
    }

    /// <summary>
    /// Removes an integration and deletes its secret. Tasks in workspaces keep naming it as their source, and find
    /// it again if the same service is connected again.
    /// </summary>
    /// <returns>Whether an integration with that ID was configured.</returns>
    /// <exception cref="CredentialStoreException">
    /// The secret couldn't be deleted, so the integration was kept. A missing credential store doesn't stop the
    /// removal, because there's nothing to delete from it.
    /// </exception>
    /// <exception cref="IOException">The settings couldn't be saved, so the integration was kept.</exception>
    public async Task<bool> RemoveAsync(string id, CancellationToken cancellationToken = default)
    {
        await _changeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            IntegrationsSettings integrations = _settingsStore.Current.Integrations;
            if (!integrations.GitHub.Any(github => github.Id == id) && !integrations.Jira.Any(jira => jira.Id == id))
            {
                return false;
            }

            try
            {
                await new IntegrationCredentials(_credentialStore, id).DeleteAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (CredentialStoreUnavailableException)
            {
                // There's no store, so no secret was saved in it.
            }

            await _settingsStore.UpdateAsync(
                settings => settings with
                {
                    Integrations = settings.Integrations with
                    {
                        GitHub = [.. settings.Integrations.GitHub.Where(github => github.Id != id)],
                        Jira = [.. settings.Integrations.Jira.Where(jira => jira.Id != id)],
                    },
                },
                CancellationToken.None).ConfigureAwait(false);

            Registry.Remove(id);
            lock (_statusLock)
            {
                _statuses.Remove(id);
            }
        }
        finally
        {
            _changeLock.Release();
        }

        Changed?.Invoke();
        return true;
    }

    private async Task<IIntegration> AddAsync(
        string id,
        string name,
        string token,
        Func<ICredentialStore, IIntegration> create,
        Func<IntegrationsSettings, IntegrationsSettings> add,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        token = token.Trim();

        await _changeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        IIntegration integration;
        try
        {
            if (Registry.Find(id) is not null)
            {
                throw new IntegrationException($"{name} is already connected.");
            }

            IntegrationStatus connected = await GetConnectedStatusAsync(
                create(new CandidateCredentialStore(token)), cancellationToken).ConfigureAwait(false);

            var credentials = new IntegrationCredentials(_credentialStore, id);
            await credentials.SaveAsync(token, cancellationToken).ConfigureAwait(false);
            try
            {
                await _settingsStore.UpdateAsync(
                    settings => settings with { Integrations = add(settings.Integrations) },
                    CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // Don't leave a secret behind for an integration that wasn't added.
                try
                {
                    await credentials.DeleteAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (CredentialStoreException)
                {
                    // Connecting the integration again replaces it.
                }

                throw;
            }

            integration = create(_credentialStore);
            Registry.Add(integration);
            lock (_statusLock)
            {
                _statuses[id] = (++_lastCheck, connected);
            }
        }
        finally
        {
            _changeLock.Release();
        }

        Changed?.Invoke();
        return integration;
    }

    private IIntegration Create(GitHubIntegrationSettings settings, ICredentialStore store) =>
        new GitHubIntegration(settings.ToOptions(), store, _httpClient, _timeProvider);

    private IIntegration Create(JiraIntegrationSettings settings, ICredentialStore store) =>
        new JiraIntegration(settings.ToOptions(), store, _httpClient, _loggerFactory.CreateLogger<JiraIntegration>(), _timeProvider);

    private static IntegrationCredentials CredentialsOf(IIntegration integration) => integration switch
    {
        GitHubIntegration github => github.Credentials,
        JiraIntegration jira => jira.Credentials,
        _ => throw new NotSupportedException($"Integration '{integration.Id}' isn't one Settings manages."),
    };

    /// <summary>
    /// Checks that the service accepts the integration's credentials, and whether they can reach everything Tammi
    /// needs.
    /// </summary>
    /// <returns>A <see cref="IntegrationConnectionState.Connected"/> status, with any warning about the credentials.</returns>
    private static async Task<IntegrationStatus> GetConnectedStatusAsync(
        IIntegration integration,
        CancellationToken cancellationToken)
    {
        string account = await GetAccountAsync(integration, cancellationToken).ConfigureAwait(false);
        string? warning = null;
        if (integration is GitHubIntegration github)
        {
            try
            {
                warning = await github.CheckTokenAccessAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (IntegrationException ex) when (ex is not IntegrationAuthenticationException)
            {
                // The credentials work, which is what the check is for. The warning is only a hint.
            }
        }

        return new IntegrationStatus(IntegrationConnectionState.Connected, account) { Warning = warning };
    }

    /// <summary>
    /// Asks the service who the credentials belong to, which checks that it accepts them.
    /// </summary>
    private static async Task<string> GetAccountAsync(IIntegration integration, CancellationToken cancellationToken)
    {
        switch (integration)
        {
            case GitHubIntegration github:
                GitHubUser user = await github.GetAuthenticatedUserAsync(cancellationToken).ConfigureAwait(false);
                return user.Login;
            case JiraIntegration jira:
                JiraUser jiraUser = await jira.GetAuthenticatedUserAsync(cancellationToken).ConfigureAwait(false);
                return string.IsNullOrWhiteSpace(jiraUser.EmailAddress)
                    ? jiraUser.DisplayName
                    : $"{jiraUser.DisplayName} ({jiraUser.EmailAddress})";
            default:
                throw new NotSupportedException($"Integration '{integration.Id}' isn't one Settings manages.");
        }
    }

    private static IntegrationStatus Failure(Exception ex) => ex is IntegrationAuthenticationException
        ? new IntegrationStatus(IntegrationConnectionState.AuthenticationFailed, Message: ex.Message)
        : new IntegrationStatus(IntegrationConnectionState.Failed, Message: ex.Message);

    private long StartCheck(string id)
    {
        long check;
        lock (_statusLock)
        {
            check = ++_lastCheck;
            _statuses[id] = (check, IntegrationStatus.Checking);
        }

        Changed?.Invoke();
        return check;
    }

    /// <summary>
    /// Records a status. With <paramref name="check"/>, it's only recorded if no later check or change has recorded
    /// one since, so a slow check can't overwrite the result of newer credentials.
    /// </summary>
    private void SetStatus(string id, IntegrationStatus status, long? check = null)
    {
        lock (_statusLock)
        {
            if (Registry.Find(id) is null
                || (check is not null && _statuses.TryGetValue(id, out var entry) && entry.Check != check))
            {
                return;
            }

            _statuses[id] = (++_lastCheck, status);
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Holds credentials that haven't been saved yet, so an integration can check them with its service first.
    /// </summary>
    private sealed class CandidateCredentialStore(string secret) : ICredentialStore
    {
        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(secret);

        public Task SaveAsync(string key, string secret, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
