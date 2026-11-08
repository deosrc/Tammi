using Deosrc.Tammi.Core.Windowing;

namespace Deosrc.Tammi.Core.Tests.Windowing;

public sealed class LayoutStateStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "tammi-tests", Guid.NewGuid().ToString("N"));

    // A folder that doesn't exist yet, as on first run.
    private string DataDirectory => Path.Combine(_root, "data");

    private string FilePath => Path.Combine(DataDirectory, LayoutStateStore.FileName);

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Current_WithoutFile_IsTheDefaults()
    {
        Assert.Equal(new LayoutState(), new LayoutStateStore(DataDirectory).Current);
        Assert.Null(new LayoutStateStore(DataDirectory).Current.GitGraphDetailsWidth);
    }

    [Fact]
    public async Task UpdateAsync_KeepsStateThatANewStoreLoads()
    {
        var store = new LayoutStateStore(DataDirectory);

        Assert.True(await store.UpdateAsync(state => state with { GitGraphDetailsWidth = 512 }, CancellationToken));

        Assert.Equal(512, store.Current.GitGraphDetailsWidth);
        Assert.Equal(512, new LayoutStateStore(DataDirectory).Current.GitGraphDetailsWidth);
        Assert.False(File.Exists(FilePath + ".tmp"));
    }

    [Fact]
    public async Task UpdateAsync_BackToTheDefault_ForgetsTheWidth()
    {
        var store = new LayoutStateStore(DataDirectory);
        await store.UpdateAsync(state => state with { GitGraphDetailsWidth = 512 }, CancellationToken);

        await store.UpdateAsync(state => state with { GitGraphDetailsWidth = null }, CancellationToken);

        Assert.Null(new LayoutStateStore(DataDirectory).Current.GitGraphDetailsWidth);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1, 2]")]
    [InlineData("""{ "version": 1, "gitGraphDetailsWidth": "wide" }""")]
    public void Current_FileThatCantBeRead_IsTheDefaults(string contents)
    {
        Directory.CreateDirectory(DataDirectory);
        File.WriteAllText(FilePath, contents);

        Assert.Equal(new LayoutState(), new LayoutStateStore(DataDirectory).Current);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-20)]
    public void Current_WidthThatIsntPositive_IsTheDefault(int width)
    {
        Directory.CreateDirectory(DataDirectory);
        File.WriteAllText(FilePath, $$"""{ "version": 1, "gitGraphDetailsWidth": {{width}} }""");

        Assert.Null(new LayoutStateStore(DataDirectory).Current.GitGraphDetailsWidth);
    }

    [Fact]
    public async Task UpdateAsync_WhenTheFileCantBeWritten_StillChangesTheCurrentState()
    {
        // A file where the folder should be, so the folder can't be created.
        Directory.CreateDirectory(_root);
        File.WriteAllText(DataDirectory, "");
        var store = new LayoutStateStore(DataDirectory);

        Assert.False(await store.UpdateAsync(state => state with { GitGraphDetailsWidth = 400 }, CancellationToken));

        Assert.Equal(400, store.Current.GitGraphDetailsWidth);
    }
}
