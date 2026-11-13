using Microsoft.Extensions.Logging;
using Deosrc.Tammi.Core.Settings;
using Deosrc.Tammi.Installation;

namespace Deosrc.Tammi.Core.Updates;

/// <summary>
/// Checks for a newer release, installs it when the user agrees, and deletes old versions when Tammi starts.
/// </summary>
/// <remarks>
/// Updating only works for a copy that was installed into a version folder (see <see cref="InstallLocation"/>), by the
/// installer or by an earlier update. A build run from source can't update, as there's nothing to replace.
/// </remarks>
public sealed class AppUpdater
{
    private readonly GitHubReleaseClient _releases;
    private readonly AppInstaller _installer;
    private readonly InstallLocation _location;
    private readonly ReleaseTarget? _target;
    private readonly ISettingsStore _settings;
    private readonly ILogger<AppUpdater> _logger;
    private readonly PackageVersion? _running;
    private readonly string? _runningDirectory;
    private readonly SemaphoreSlim _busy = new(1, 1);
    private bool _started;
    private bool _installFailed;
    private PackageVersion? _dismissed;

    public AppUpdater(
        HttpClient http,
        InstallLocation location,
        ReleaseTarget? target,
        ISettingsStore settings,
        ILogger<AppUpdater> logger,
        string? runningDirectory = null)
    {
        _location = location;
        _target = target;
        _settings = settings;
        _logger = logger;
        _releases = new GitHubReleaseClient(http);
        _installer = new AppInstaller(http, location, target ?? new ReleaseTarget("linux-x64"));
        _runningDirectory = runningDirectory ?? AppContext.BaseDirectory;
        _running = target is null ? null : location.VersionRunningFrom(_runningDirectory);
    }

    /// <summary>
    /// Raised when <see cref="Status"/>, <see cref="Progress"/> or <see cref="Available"/> changes, on any thread.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// Whether this copy of Tammi can update itself. It can't if it isn't running from a version folder.
    /// </summary>
    public bool CanUpdate => _running is not null;

    /// <summary>
    /// The version that's running.
    /// </summary>
    public string CurrentVersion => AppInfo.Version;

    public UpdateStatus Status { get; private set; }

    /// <summary>
    /// The newer release, once one has been found.
    /// </summary>
    public ReleaseInfo? Available { get; private set; }

    /// <summary>
    /// How much of the download has arrived, from 0 to 1.
    /// </summary>
    public double Progress { get; private set; }

    /// <summary>
    /// What went wrong when <see cref="Status"/> is <see cref="UpdateStatus.Failed"/>.
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// Whether the prompt should be shown: a newer release is waiting for an answer, is being installed, was
    /// installed, or couldn't be installed.
    /// </summary>
    public bool ShowPrompt => Status switch
    {
        UpdateStatus.Available => Available?.Version != _dismissed,
        UpdateStatus.Installing or UpdateStatus.Installed => true,
        UpdateStatus.Failed => _installFailed,
        _ => false,
    };

    /// <summary>
    /// Runs the work done when Tammi starts, once: deletes old versions, then looks for an update if the settings say to.
    /// </summary>
    public async Task StartAsync()
    {
        if (_started || !CanUpdate)
        {
            return;
        }

        _started = true;
        PointMenuLink();
        await Task.Run(CleanUp);
        if (_settings.Current.Updates.CheckOnStartup)
        {
            await CheckAsync();
        }
    }

    private void PointMenuLink()
    {
        if (_running is null || _target is null)
        {
            return;
        }

        try
        {
            var executable = Path.Combine(_runningDirectory!, _target.ExecutableName);
            if (File.Exists(executable))
            {
                MenuLink.Point(_target, executable);
                new InstallMetadata(_running.ToString(), _target.RuntimeIdentifier).Save(_runningDirectory!);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Couldn't create or update the application menu link");
        }
    }

    private void CleanUp()
    {
        try
        {
            var deleted = VersionCleaner.Clean(_location, _running);
            if (deleted.Count > 0)
            {
                _logger.LogInformation("Deleted old versions: {Versions}", string.Join(", ", deleted));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Couldn't delete old versions");
        }
    }

    /// <summary>
    /// Looks for a release newer than the running one.
    /// </summary>
    public async Task CheckAsync(CancellationToken cancellationToken = default)
    {
        if (_running is null || !await _busy.WaitAsync(0, cancellationToken))
        {
            return;
        }

        try
        {
            if (Status is UpdateStatus.Installing or UpdateStatus.Installed)
            {
                return;
            }

            _installFailed = false;
            Set(UpdateStatus.Checking);
            var latest = await _releases.GetLatestAsync(_settings.Current.Updates.IncludePreReleases, cancellationToken);
            if (latest is not null && latest.Version > _running)
            {
                _logger.LogInformation("Tammi {Version} is available (running {Running})", latest.Version, _running);
                Available = latest;
                Set(UpdateStatus.Available);
            }
            else
            {
                Available = null;
                Set(UpdateStatus.Idle);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            _logger.LogWarning(ex, "Couldn't check for updates");
            Fail($"Couldn't check for updates. {ex.Message}");
        }
        finally
        {
            _busy.Release();
        }
    }

    /// <summary>
    /// Downloads and installs the newer release, and points the menu link at it.
    /// </summary>
    public async Task InstallAsync(CancellationToken cancellationToken = default)
    {
        if (_running is null || Available is not { } release || !await _busy.WaitAsync(0, cancellationToken))
        {
            return;
        }

        try
        {
            Progress = 0;
            _installFailed = false;
            Set(UpdateStatus.Installing);
            await _installer.InstallAsync(
                release,
                new Progress<DownloadProgress>(progress =>
                {
                    Progress = progress.TotalBytes > 0 ? (double)progress.BytesReceived / progress.TotalBytes : 0;
                    Changed?.Invoke();
                }),
                cancellationToken);
            _logger.LogInformation("Installed Tammi {Version}", release.Version);
            Set(UpdateStatus.Installed);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException
            or UnauthorizedAccessException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Couldn't install Tammi {Version}", release.Version);
            _installFailed = true;
            Fail($"Couldn't install {release.Version}. {ex.Message}");
        }
        finally
        {
            _busy.Release();
        }
    }

    /// <summary>
    /// Stops asking about the available release until a newer one is found.
    /// </summary>
    public void Dismiss()
    {
        _dismissed = Available?.Version;
        Changed?.Invoke();
    }

    private void Set(UpdateStatus status)
    {
        Status = status;
        Error = null;
        Changed?.Invoke();
    }

    private void Fail(string error)
    {
        Status = UpdateStatus.Failed;
        Error = error;
        Changed?.Invoke();
    }
}
