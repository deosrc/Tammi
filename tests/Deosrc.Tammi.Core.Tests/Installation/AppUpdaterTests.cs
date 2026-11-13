using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Deosrc.Tammi.Core.Settings;
using Deosrc.Tammi.Core.Tests.Settings;
using Deosrc.Tammi.Core.Tests.Tools;
using Deosrc.Tammi.Core.Updates;
using Deosrc.Tammi.Core.Installation;

namespace Deosrc.Tammi.Core.Tests.Installation;

public class AppUpdaterTests
{
    private const string ReleasesJson = """
        [
          { "tag_name": "v0.3.0-beta.1", "draft": false, "html_url": "https://example.com/3", "assets": [] },
          { "tag_name": "v0.2.0", "draft": false, "html_url": "https://example.com/2", "assets": [] },
          { "tag_name": "v0.1.0", "draft": false, "html_url": "https://example.com/1", "assets": [] }
        ]
        """;

    private sealed class FakeHandler : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ReleasesJson) });
        }
    }

    private static (AppUpdater Updater, FakeHandler Handler) Create(
        TempFolder folder, string running, UpdateSettings? settings = null, string? runningFolder = null)
    {
        var handler = new FakeHandler();
        var store = new InMemorySettingsStore(new AppSettings { Updates = settings ?? new UpdateSettings() });
        var updater = new AppUpdater(
            new HttpClient(handler),
            new InstallLocation(folder.Path),
            new ReleaseTarget("linux-x64"),
            store,
            NullLogger<AppUpdater>.Instance,
            runningFolder ?? Path.Combine(folder.Path, running));
        return (updater, handler);
    }

    [Fact]
    public async Task FindsANewerStableRelease()
    {
        using var folder = new TempFolder();
        var (updater, _) = Create(folder, "0.1.0");

        await updater.CheckAsync(TestContext.Current.CancellationToken);

        Assert.Equal(UpdateStatus.Available, updater.Status);
        Assert.Equal("v0.2.0", updater.Available!.Tag);
        Assert.True(updater.ShowPrompt);
    }

    [Fact]
    public async Task FindsAPreReleaseWhenTheSettingAllowsIt()
    {
        using var folder = new TempFolder();
        var (updater, _) = Create(folder, "0.1.0", new UpdateSettings { IncludePreReleases = true });

        await updater.CheckAsync(TestContext.Current.CancellationToken);

        Assert.Equal("v0.3.0-beta.1", updater.Available!.Tag);
    }

    [Fact]
    public async Task IsIdleWhenRunningTheNewestRelease()
    {
        using var folder = new TempFolder();
        var (updater, _) = Create(folder, "0.2.0");

        await updater.CheckAsync(TestContext.Current.CancellationToken);

        Assert.Equal(UpdateStatus.Idle, updater.Status);
        Assert.False(updater.ShowPrompt);
    }

    [Fact]
    public async Task DoesNotPromptAgainAfterNotNow()
    {
        using var folder = new TempFolder();
        var (updater, _) = Create(folder, "0.1.0");
        await updater.CheckAsync(TestContext.Current.CancellationToken);

        updater.Dismiss();

        Assert.False(updater.ShowPrompt);
    }

    [Fact]
    public async Task CannotUpdateOutsideAVersionFolder()
    {
        using var folder = new TempFolder();
        var (updater, handler) = Create(folder, "0.1.0", runningFolder: Path.Combine(Path.GetTempPath(), "somewhere-else"));

        await updater.CheckAsync(TestContext.Current.CancellationToken);
        await updater.StartAsync();

        Assert.False(updater.CanUpdate);
        Assert.Equal(0, handler.Requests);
    }

    [Fact]
    public async Task StartDeletesOldVersionsAndChecksForUpdates()
    {
        using var folder = new TempFolder();
        foreach (var version in new[] { "0.0.1", "0.0.2", "0.0.3", "0.1.0" })
        {
            folder.AddFile(Path.Combine(version, InstallLocation.MetadataFileName), "{}");
        }

        var (updater, _) = Create(folder, "0.1.0");

        await updater.StartAsync();

        Assert.False(Directory.Exists(Path.Combine(folder.Path, "0.0.1")));
        Assert.True(Directory.Exists(Path.Combine(folder.Path, "0.0.2")));
        Assert.Equal(UpdateStatus.Available, updater.Status);
    }

    [Fact]
    public async Task StartDoesNotCheckWhenTurnedOff()
    {
        using var folder = new TempFolder();
        var (updater, handler) = Create(folder, "0.1.0", new UpdateSettings { CheckOnStartup = false });

        await updater.StartAsync();

        Assert.Equal(0, handler.Requests);
    }
}
