using Deosrc.Tammi.Core.Platform;
using Deosrc.Tammi.Core.Tests.Tools;

namespace Deosrc.Tammi.Core.Tests.Platform;

public sealed class DesktopEntryIconProviderTests : IDisposable
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4e, 0x47];

    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    private string UserData => Path.Combine(_folder.Path, "home", ".local", "share");

    private string SystemData => Path.Combine(_folder.Path, "usr", "share");

    private DesktopEntryIconProvider CreateProvider() => new([UserData, SystemData]);

    [Fact]
    public async Task GetIconAsync_FindsThemeIconOfEntryWithSameCommandName()
    {
        AddEntry(SystemData, "code.desktop", "Exec=/usr/share/code/code %F", "Icon=vscode");
        var icon = AddImage(SystemData, "icons/hicolor/256x256/apps/vscode.png");

        var dataUri = await CreateProvider().GetIconAsync("/usr/bin/code", TestContext.Current.CancellationToken);

        Assert.Equal(DataUri("image/png", icon), dataUri);
    }

    [Fact]
    public async Task GetIconAsync_IgnoresShExtension_AndQuotes()
    {
        AddEntry(UserData, "jetbrains-rider.desktop", "Exec=\"/opt/Rider 2025/bin/rider.sh\" %u", "Icon=rider");
        AddImage(SystemData, "pixmaps/rider.png");

        Assert.NotNull(await CreateProvider().GetIconAsync("/home/me/scripts/rider", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetIconAsync_PrefersScalableThenLargestIcon()
    {
        AddEntry(SystemData, "tool.desktop", "Exec=tool", "Icon=tool");
        AddImage(SystemData, "icons/hicolor/48x48/apps/tool.png");
        var large = AddImage(SystemData, "icons/hicolor/128x128/apps/tool.png");

        Assert.Equal(
            DataUri("image/png", large),
            await CreateProvider().GetIconAsync("/usr/bin/tool", TestContext.Current.CancellationToken));

        var scalable = AddImage(SystemData, "icons/hicolor/scalable/apps/tool.svg", "<svg/>"u8.ToArray());
        Assert.Equal(
            DataUri("image/svg+xml", scalable),
            await CreateProvider().GetIconAsync("/usr/bin/tool", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetIconAsync_UsesIconPath()
    {
        var icon = AddImage(_folder.Path, "snap/tool/icon.png");
        AddEntry(SystemData, "tool.desktop", "Exec=tool", $"Icon={icon}");

        Assert.Equal(
            DataUri("image/png", icon),
            await CreateProvider().GetIconAsync("/snap/bin/tool", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetIconAsync_PrefersEntryForSameFile_OverSameName()
    {
        var executable = Path.Combine(_folder.Path, "bin", "tool");
        Directory.CreateDirectory(Path.GetDirectoryName(executable)!);
        File.WriteAllText(executable, "");
        AddEntry(SystemData, "a-other.desktop", "Exec=/elsewhere/tool", "Icon=other");
        AddEntry(SystemData, "b-tool.desktop", $"TryExec={executable}", "Exec=launch", "Icon=right");
        AddImage(SystemData, "pixmaps/other.png");
        var right = AddImage(SystemData, "pixmaps/right.png");

        Assert.Equal(
            DataUri("image/png", right),
            await CreateProvider().GetIconAsync(executable, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetIconAsync_OnlyReadsMainGroup()
    {
        AddEntry(SystemData, "tool.desktop", "Exec=other", "Icon=tool", "", "[Desktop Action New]", "Exec=tool");
        AddImage(SystemData, "pixmaps/tool.png");

        Assert.Null(await CreateProvider().GetIconAsync("/usr/bin/tool", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetIconAsync_WithoutEntryOrIconFile_IsNull()
    {
        AddEntry(SystemData, "tool.desktop", "Exec=tool", "Icon=missing");

        Assert.Null(await CreateProvider().GetIconAsync("/usr/bin/tool", TestContext.Current.CancellationToken));
        Assert.Null(await CreateProvider().GetIconAsync("/usr/bin/other", TestContext.Current.CancellationToken));
    }

    private static string DataUri(string mediaType, string path) =>
        ImageDataUri.FromBytes(mediaType, File.ReadAllBytes(path));

    private static void AddEntry(string dataDirectory, string name, params string[] lines)
    {
        var path = Path.Combine(dataDirectory, "applications", name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllLines(path, ["[Desktop Entry]", "Type=Application", .. lines]);
    }

    private static string AddImage(string root, string relativePath, byte[]? contents = null)
    {
        var path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, contents ?? [.. Png, .. System.Text.Encoding.UTF8.GetBytes(relativePath)]);
        return path;
    }
}
