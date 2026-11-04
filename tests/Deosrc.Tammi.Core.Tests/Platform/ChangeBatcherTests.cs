using Microsoft.Extensions.Time.Testing;
using Tammi.Core.Platform;

namespace Tammi.Core.Tests.Platform;

public class ChangeBatcherTests
{
    private static readonly TimeSpan QuietPeriod = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromMilliseconds(1000);

    private readonly FakeTimeProvider _time = new();
    private readonly List<FileSystemChanges> _batches = [];

    [Fact]
    public void ReportsChangesAfterQuietPeriod()
    {
        using var batcher = CreateBatcher();

        batcher.AddPath("a");
        _time.Advance(QuietPeriod - TimeSpan.FromMilliseconds(1));
        Assert.Empty(_batches);

        _time.Advance(TimeSpan.FromMilliseconds(1));
        var batch = Assert.Single(_batches);
        Assert.Equal(["a"], batch.Paths);
        Assert.False(batch.RescanRequired);
    }

    [Fact]
    public void EachChangeRestartsQuietPeriod()
    {
        using var batcher = CreateBatcher();

        batcher.AddPath("a");
        _time.Advance(TimeSpan.FromMilliseconds(80));
        batcher.AddPath("b");
        _time.Advance(TimeSpan.FromMilliseconds(80));
        Assert.Empty(_batches);

        _time.Advance(TimeSpan.FromMilliseconds(20));
        var batch = Assert.Single(_batches);
        Assert.Equal(["a", "b"], batch.Paths.Order());
    }

    [Fact]
    public void ReportsDuplicatePathsOnce()
    {
        using var batcher = CreateBatcher();

        batcher.AddPath("a");
        batcher.AddPath("a");
        _time.Advance(QuietPeriod);

        Assert.Equal(["a"], Assert.Single(_batches).Paths);
    }

    [Fact]
    public void ReportsChangesAtMaxDelay_WhenChangesDoNotStop()
    {
        using var batcher = CreateBatcher();

        for (var elapsed = 0; elapsed < 1000; elapsed += 50)
        {
            batcher.AddPath($"file{elapsed}");
            _time.Advance(TimeSpan.FromMilliseconds(50));
        }

        var batch = Assert.Single(_batches);
        Assert.Equal(20, batch.Paths.Count);
    }

    [Fact]
    public void StartsNewBatchAfterReporting()
    {
        using var batcher = CreateBatcher();

        batcher.AddPath("a");
        _time.Advance(QuietPeriod);
        batcher.AddPath("b");
        _time.Advance(QuietPeriod);

        Assert.Collection(
            _batches,
            first => Assert.Equal(["a"], first.Paths),
            second => Assert.Equal(["b"], second.Paths));
    }

    [Fact]
    public void ReportsRescanWithFirstError()
    {
        using var batcher = CreateBatcher();
        var first = new IOException("first");

        batcher.AddPath("a");
        batcher.AddRescan(first);
        batcher.AddRescan(new IOException("second"));
        _time.Advance(QuietPeriod);

        var batch = Assert.Single(_batches);
        Assert.True(batch.RescanRequired);
        Assert.Same(first, batch.Error);
        Assert.Equal(["a"], batch.Paths);
    }

    [Fact]
    public void ReportsNothingAfterDispose()
    {
        var batcher = CreateBatcher();

        batcher.AddPath("a");
        batcher.Dispose();
        batcher.AddPath("b");
        _time.Advance(MaxDelay);

        Assert.Empty(_batches);
    }

    private ChangeBatcher CreateBatcher() => new(QuietPeriod, MaxDelay, _time, _batches.Add);
}
