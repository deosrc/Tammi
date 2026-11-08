using Deosrc.Tammi.Core.Platform;
using Deosrc.Tammi.Core.Tools;

namespace Deosrc.Tammi.Core.Tests.Tools;

public sealed class ToolImagesTests : IDisposable
{
    private readonly TempFolder _folder = new();
    private readonly FakeIconProvider _icons = new();

    public void Dispose() => _folder.Dispose();

    private static ToolDefinition Tool(ToolIconSource source, string? customPath = null) => new()
    {
        Icon = new ToolIconSettings { Source = source, Generic = ToolIcon.Editor, CustomPath = customPath },
    };

    [Fact]
    public async Task GetAsync_Application_UsesExecutablesIcon()
    {
        var image = await new ToolImages(_icons).GetAsync(Tool(ToolIconSource.Application), "/usr/bin/code");

        Assert.Equal(new ToolImage("data:icon/usr/bin/code", ToolIcon.Editor), image);
    }

    [Fact]
    public async Task GetAsync_Application_WithoutExecutableOrIcon_UsesGenericIcon()
    {
        var images = new ToolImages(new FakeIconProvider { Icon = null });

        Assert.Equal(new ToolImage(null, ToolIcon.Editor), await images.GetAsync(Tool(ToolIconSource.Application), "/x"));
        Assert.Equal(new ToolImage(null, ToolIcon.Editor), await images.GetAsync(Tool(ToolIconSource.Application), null));
    }

    [Fact]
    public async Task GetAsync_Application_WhenProviderFails_UsesGenericIcon()
    {
        var images = new ToolImages(new FakeIconProvider { Exception = new IOException("No") });

        Assert.Null((await images.GetAsync(Tool(ToolIconSource.Application), "/x")).DataUri);
    }

    [Fact]
    public async Task GetAsync_Application_IsReadOnceUntilCleared()
    {
        var images = new ToolImages(_icons);

        await images.GetAsync(Tool(ToolIconSource.Application), "/usr/bin/code");
        await images.GetAsync(Tool(ToolIconSource.Application), "/usr/bin/code");
        Assert.Equal(1, _icons.Calls);

        images.Clear();
        await images.GetAsync(Tool(ToolIconSource.Application), "/usr/bin/code");
        Assert.Equal(2, _icons.Calls);
    }

    [Fact]
    public async Task GetAsync_Generic_UsesGenericIcon()
    {
        var image = await new ToolImages(_icons).GetAsync(Tool(ToolIconSource.Generic), "/usr/bin/code");

        Assert.Equal(new ToolImage(null, ToolIcon.Editor), image);
        Assert.Equal(0, _icons.Calls);
    }

    [Fact]
    public async Task GetAsync_Custom_ReadsImageFile()
    {
        var path = _folder.AddFile("icon.svg", "<svg/>");

        var image = await new ToolImages(_icons).GetAsync(Tool(ToolIconSource.Custom, path), "/usr/bin/code");

        Assert.Equal(ImageDataUri.FromBytes("image/svg+xml", "<svg/>"u8.ToArray()), image.DataUri);
    }

    [Theory]
    [InlineData("missing.png")]
    [InlineData("notes.txt")]
    [InlineData("empty.png")]
    public async Task GetAsync_Custom_WithUnreadableFile_UsesGenericIcon(string name)
    {
        _folder.AddFile("notes.txt", "text");
        _folder.AddFile("empty.png");

        var image = await new ToolImages(_icons).GetAsync(
            Tool(ToolIconSource.Custom, Path.Combine(_folder.Path, name)), "/usr/bin/code");

        Assert.Equal(new ToolImage(null, ToolIcon.Editor), image);
    }

    private sealed class FakeIconProvider : IApplicationIconProvider
    {
        public string? Icon { get; init; } = "data:icon";

        public Exception? Exception { get; init; }

        public int Calls { get; private set; }

        public Task<string?> GetIconAsync(string executablePath, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Exception is null
                ? Task.FromResult(Icon is null ? null : Icon + executablePath)
                : Task.FromException<string?>(Exception);
        }
    }
}
