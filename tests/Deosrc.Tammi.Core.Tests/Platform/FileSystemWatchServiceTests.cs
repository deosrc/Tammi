using TAMMi.Core.Platform;

namespace TAMMi.Core.Tests.Platform;

public sealed class FileSystemWatchServiceTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("tammi-tests-");
    private readonly FileSystemWatchService _service = new();

    public void Dispose() => _directory.Delete(recursive: true);

    [Fact]
    public async Task ReportsChangesInSubdirectories()
    {
        var subdirectory = _directory.CreateSubdirectory("repo");
        var changes = new TaskCompletionSource<FileSystemChanges>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var watch = _service.Watch(
            _directory.FullName,
            batch => changes.TrySetResult(batch),
            new FileSystemWatchOptions { QuietPeriod = TimeSpan.FromMilliseconds(50) });

        var file = Path.Combine(subdirectory.FullName, "file.txt");
        await File.WriteAllTextAsync(file, "content", TestContext.Current.CancellationToken);

        var batch = await changes.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        Assert.Contains(file, batch.Paths);
    }

    [Fact]
    public void Watch_Throws_WhenDirectoryIsMissing()
    {
        var missing = Path.Combine(_directory.FullName, "missing");

        Assert.Throws<DirectoryNotFoundException>(() => _service.Watch(missing, _ => { }));
    }
}
