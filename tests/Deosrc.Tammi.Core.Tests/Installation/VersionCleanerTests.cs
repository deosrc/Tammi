using Deosrc.Tammi.Core.Tests.Tools;
using Deosrc.Tammi.Installation;

namespace Deosrc.Tammi.Core.Tests.Installation;

public class VersionCleanerTests
{
    private static void Install(TempFolder folder, params string[] versions)
    {
        foreach (var version in versions)
        {
            folder.AddFile(Path.Combine(version, InstallLocation.MetadataFileName), "{}");
        }
    }

    [Fact]
    public void KeepsTheRunningVersionAndTheTwoBeforeIt()
    {
        using var folder = new TempFolder();
        Install(folder, "0.1.0", "0.2.0", "0.3.0", "0.4.0", "0.5.0");
        var location = new InstallLocation(folder.Path);

        var deleted = VersionCleaner.Clean(location, PackageVersion.Parse("0.5.0"));

        Assert.Equal(["0.2.0", "0.1.0"], deleted.OrderByDescending(v => v).Select(v => v.ToString()));
        Assert.Equal(["0.5.0", "0.4.0", "0.3.0"], location.InstalledVersions().Select(v => v.ToString()));
    }

    [Fact]
    public void KeepsVersionsNewerThanTheRunningOne()
    {
        using var folder = new TempFolder();
        Install(folder, "0.1.0", "0.2.0", "0.3.0", "0.4.0", "0.5.0");
        var location = new InstallLocation(folder.Path);

        VersionCleaner.Clean(location, PackageVersion.Parse("0.3.0"));

        Assert.Equal(["0.5.0", "0.4.0", "0.3.0", "0.2.0", "0.1.0"], location.InstalledVersions().Select(v => v.ToString()));
    }

    [Fact]
    public void LeavesUnfinishedInstallsAndOtherFoldersAlone()
    {
        using var folder = new TempFolder();
        Install(folder, "0.1.0", "0.2.0", "0.3.0", "0.4.0");
        folder.AddFile(Path.Combine("0.0.1", "app.dll"));
        folder.AddFile(Path.Combine("0.0.2.partial", "app.dll"));
        folder.AddFile(Path.Combine("notes", "readme.txt"));
        var location = new InstallLocation(folder.Path);

        VersionCleaner.Clean(location, PackageVersion.Parse("0.4.0"));

        Assert.True(Directory.Exists(Path.Combine(folder.Path, "0.0.1")));
        Assert.True(Directory.Exists(Path.Combine(folder.Path, "0.0.2.partial")));
        Assert.True(Directory.Exists(Path.Combine(folder.Path, "notes")));
        Assert.False(Directory.Exists(Path.Combine(folder.Path, "0.1.0")));
    }

    [Fact]
    public void DoesNothingWithoutARunningVersion()
    {
        using var folder = new TempFolder();
        Install(folder, "0.1.0", "0.2.0", "0.3.0", "0.4.0");

        Assert.Empty(VersionCleaner.Clean(new InstallLocation(folder.Path), running: null));
    }

    [Fact]
    public void FindsTheVersionAFolderBelongsTo()
    {
        using var folder = new TempFolder();
        var location = new InstallLocation(folder.Path);

        Assert.Equal("0.2.0", location.VersionRunningFrom(Path.Combine(folder.Path, "0.2.0", "wwwroot"))?.ToString());
        Assert.Null(location.VersionRunningFrom(Path.Combine(folder.Path, "..", "bin", "Debug")));
    }
}
