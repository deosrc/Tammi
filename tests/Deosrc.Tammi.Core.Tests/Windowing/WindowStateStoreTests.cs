using Deosrc.Tammi.Core.Windowing;

namespace Deosrc.Tammi.Core.Tests.Windowing;

public sealed class WindowStateStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "tammi-tests", Guid.NewGuid().ToString("N"));

    // A folder that doesn't exist yet, as on first run.
    private string DataDirectory => Path.Combine(_root, "data");

    private string FilePath => Path.Combine(DataDirectory, WindowStateStore.FileName);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Load_WithoutFile_ReturnsNull()
    {
        Assert.Null(new WindowStateStore(DataDirectory).Load());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Save_KeepsStateThatANewStoreLoads(bool maximized)
    {
        var state = new WindowState(1400, 900, maximized);

        Assert.True(new WindowStateStore(DataDirectory).Save(state));

        Assert.Equal(state, new WindowStateStore(DataDirectory).Load());
        Assert.False(File.Exists(FilePath + ".tmp"));
    }

    [Fact]
    public void Save_ReplacesEarlierState()
    {
        var store = new WindowStateStore(DataDirectory);
        store.Save(new WindowState(1400, 900, IsMaximized: true));

        store.Save(new WindowState(1000, 700, IsMaximized: false));

        Assert.Equal(new WindowState(1000, 700, IsMaximized: false), store.Load());
    }

    [Fact]
    public void Save_WritesVersion()
    {
        new WindowStateStore(DataDirectory).Save(new WindowState(1400, 900, IsMaximized: false));

        Assert.Contains("\"version\": 1", File.ReadAllText(FilePath));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{ "version": 2, "width": 1400, "height": 900, "isMaximized": false }""")]
    [InlineData("""{ "width": 1400, "height": 900, "isMaximized": false }""")]
    [InlineData("""{ "version": 1, "width": 0, "height": 900, "isMaximized": false }""")]
    [InlineData("""{ "version": 1, "width": 1400, "height": -1, "isMaximized": false }""")]
    [InlineData("""{ "version": 1, "width": "wide", "height": 900, "isMaximized": false }""")]
    public void Load_WithInvalidFile_ReturnsNull(string contents)
    {
        Directory.CreateDirectory(DataDirectory);
        File.WriteAllText(FilePath, contents);

        Assert.Null(new WindowStateStore(DataDirectory).Load());
    }

    [Fact]
    public void Save_WhenFolderCantBeCreated_ReturnsFalse()
    {
        // A file where the folder should be.
        Directory.CreateDirectory(_root);
        File.WriteAllText(DataDirectory, "");

        Assert.False(new WindowStateStore(DataDirectory).Save(new WindowState(1400, 900, IsMaximized: false)));
    }
}
