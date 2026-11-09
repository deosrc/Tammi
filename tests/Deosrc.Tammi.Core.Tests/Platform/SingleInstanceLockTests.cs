using Deosrc.Tammi.Core.Platform;

namespace Deosrc.Tammi.Core.Tests.Platform;

public sealed class SingleInstanceLockTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "tammi-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void TryAcquire_WhileHeld_ReturnsNull_AndWorksAgainOnceReleased()
    {
        var first = SingleInstanceLock.TryAcquire(_directory);
        Assert.NotNull(first);

        Assert.Null(SingleInstanceLock.TryAcquire(_directory));

        first.Dispose();
        using var second = SingleInstanceLock.TryAcquire(_directory);
        Assert.NotNull(second);
    }
}
