using System.Net.Http.Headers;
using Deosrc.Tammi.Installation;
using Deosrc.Tammi.Installer;

var options = InstallerOptions.Parse(args, out var optionsError);
if (options is null)
{
    Console.Error.WriteLine(optionsError);
    Console.Error.WriteLine();
    Console.Error.WriteLine(InstallerOptions.Usage);
    return 2;
}

if (options.ShowHelp)
{
    Console.WriteLine(InstallerOptions.Usage);
    return 0;
}

try
{
    return await InstallAsync(options);
}
catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException
    or PlatformNotSupportedException or UnauthorizedAccessException or TaskCanceledException)
{
    Console.Error.WriteLine($"Installation failed: {ex.Message}");
    return 1;
}

static async Task<int> InstallAsync(InstallerOptions options)
{
    var target = ReleaseTarget.Current();
    var location = InstallLocation.Default();
    using var http = new HttpClient();
    http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("tammi-installer", "1.0"));
    var client = new GitHubReleaseClient(http);

    Console.WriteLine("Looking for the release to install...");
    var release = options.Tag is { } tag
        ? await client.GetByTagAsync(tag)
        : await client.GetLatestAsync(options.IncludePreReleases);
    if (release is null)
    {
        Console.Error.WriteLine(options.Tag is { } missing
            ? $"There's no release tagged '{missing}'."
            : "There are no releases to install.");
        return 1;
    }

    var problems = DependencyChecker.Check();
    if (problems.Count > 0)
    {
        Console.Error.WriteLine("Tammi can't be installed yet. Fix the following, then run the installer again:");
        foreach (var problem in problems)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine($"  {problem.Name}: {problem.Problem}");
            Console.Error.WriteLine($"    {problem.Fix}");
        }

        return 1;
    }

    Console.WriteLine($"Installing Tammi {release.Version} ({target.RuntimeIdentifier})...");
    var installer = new AppInstaller(http, location, target);
    var lastPercent = -1;
    var executable = await installer.InstallAsync(
        release,
        new Progress<DownloadProgress>(progress =>
        {
            var percent = progress.TotalBytes > 0 ? (int)(progress.BytesReceived * 100 / progress.TotalBytes) : 0;
            if (percent / 10 != lastPercent / 10)
            {
                lastPercent = percent;
                Console.WriteLine($"  Downloaded {percent}%");
            }
        }));

    Console.WriteLine();
    Console.WriteLine($"Tammi {release.Version} is installed at {executable}");
    Console.WriteLine($"It's in your application menu ({MenuLink.CurrentPath(target)}). Tammi offers updates itself.");
    return 0;
}
