namespace Deosrc.Tammi.Core.Tests.Tools;

/// <summary>
/// A folder for a test's files, deleted when the test ends.
/// </summary>
internal sealed class TempFolder : IDisposable
{
    public string Path { get; } =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tammi-tests", Guid.NewGuid().ToString("N"));

    public TempFolder() => Directory.CreateDirectory(Path);

    /// <summary>
    /// Creates a file, and its folders, at a path relative to the folder.
    /// </summary>
    /// <returns>The file's full path.</returns>
    public string AddFile(string relativePath, string contents = "")
    {
        var path = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
