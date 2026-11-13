using System.Net.Http.Headers;

namespace Deosrc.Tammi.Installation;

/// <summary>
/// Installs a release into a versioned folder and points the menu link at it. Initial setup and an update are the
/// same operation, so the installer app and Tammi's updater both use this.
/// </summary>
public sealed class AppInstaller
{
    private readonly HttpClient _http;
    private readonly InstallLocation _location;
    private readonly ReleaseTarget _target;

    public AppInstaller(HttpClient http, InstallLocation location, ReleaseTarget target)
    {
        _http = http;
        _location = location;
        _target = target;
    }

    /// <summary>
    /// Downloads and unpacks a release, then points the menu link at it. A version that's already installed
    /// isn't downloaded again.
    /// </summary>
    /// <returns>The path of the installed version's executable.</returns>
    /// <exception cref="InvalidOperationException">The release has no archive for this OS and architecture.</exception>
    public async Task<string> InstallAsync(
        ReleaseInfo release,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var directory = _location.DirectoryFor(release.Version);
        if (!_location.IsInstalled(release.Version))
        {
            var archive = release.FindArchive(_target)
                ?? throw new InvalidOperationException(
                    $"Release {release.Tag} has no download for {_target.RuntimeIdentifier}.");
            await DownloadAndExtractAsync(archive, directory, progress, cancellationToken);
            new InstallMetadata(release.Version.ToString(), _target.RuntimeIdentifier).Save(directory);
        }

        var executable = Path.Combine(directory, _target.ExecutableName);
        if (!File.Exists(executable))
        {
            throw new InvalidOperationException($"'{_target.ExecutableName}' wasn't found in the downloaded release.");
        }

        MenuLink.Point(_target, executable);
        return executable;
    }

    private async Task DownloadAndExtractAsync(
        ReleaseAsset archive,
        string directory,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_location.VersionsDirectory);
        var work = directory + ".partial";
        var file = Path.Combine(_location.VersionsDirectory, archive.Name);
        try
        {
            if (Directory.Exists(work))
            {
                Directory.Delete(work, recursive: true);
            }

            await DownloadAsync(archive, file, progress, cancellationToken);
            // Unpack beside the destination, then rename, so an interrupted install never leaves a folder that
            // looks complete.
            ArchiveExtractor.Extract(file, work);
            if (!OperatingSystem.IsWindows() && !_target.IsWindows)
            {
                var executable = Path.Combine(work, _target.ExecutableName);
                if (File.Exists(executable))
                {
                    File.SetUnixFileMode(executable, File.GetUnixFileMode(executable)
                        | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
                }
            }

            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }

            Directory.Move(work, directory);
        }
        finally
        {
            File.Delete(file);
            if (Directory.Exists(work))
            {
                Directory.Delete(work, recursive: true);
            }
        }
    }

    private async Task DownloadAsync(
        ReleaseAsset archive, string file, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, archive.DownloadUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength ?? archive.Size;
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destination = File.Create(file);
        var buffer = new byte[81920];
        long received = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            received += read;
            progress?.Report(new DownloadProgress(received, total));
        }
    }
}
