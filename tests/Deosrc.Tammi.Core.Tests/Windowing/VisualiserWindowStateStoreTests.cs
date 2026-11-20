using Deosrc.Tammi.Core.Windowing;

namespace Deosrc.Tammi.Core.Tests.Windowing;

public sealed class VisualiserWindowStateStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "tammi-tests", Guid.NewGuid().ToString("N"));

    // A folder that doesn't exist yet, as on first run.
    private string DataDirectory => Path.Combine(_root, "data");

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
        Assert.Null(new VisualiserWindowStateStore(DataDirectory).Load());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Save_KeepsStateThatANewStoreLoads(bool maximized)
    {
        var state = new VisualiserWindowState(1400, 900, 120, -30, maximized);

        Assert.True(new VisualiserWindowStateStore(DataDirectory).Save(state));

        Assert.Equal(state, new VisualiserWindowStateStore(DataDirectory).Load());
    }

    [Fact]
    public void Save_KeepsAnUnknownPosition()
    {
        var state = new VisualiserWindowState(1400, 900, X: null, Y: null, IsMaximized: false);

        new VisualiserWindowStateStore(DataDirectory).Save(state);

        Assert.Equal(state, new VisualiserWindowStateStore(DataDirectory).Load());
    }

    [Fact]
    public void Load_WithAnUnreadableFile_ReturnsNull()
    {
        Directory.CreateDirectory(DataDirectory);
        File.WriteAllText(Path.Combine(DataDirectory, VisualiserWindowStateStore.FileName), "not json");

        Assert.Null(new VisualiserWindowStateStore(DataDirectory).Load());
    }
}
