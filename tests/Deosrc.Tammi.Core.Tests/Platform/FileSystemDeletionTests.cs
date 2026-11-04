using Tammi.Core.Platform;

namespace Tammi.Core.Tests.Platform;

public sealed class FileSystemDeletionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "tammi-tests", Guid.NewGuid().ToString("N"));

    public FileSystemDeletionTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            FileSystemDeletion.DeleteFolder(_root);
        }
    }

    [Fact]
    public void DeletesNestedAndReadOnlyFiles()
    {
        string folder = Path.Combine(_root, "folder");
        string file = Path.Combine(folder, "a", "b", "object");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, "data");
        File.SetAttributes(file, FileAttributes.ReadOnly);

        FileSystemDeletion.DeleteFolder(folder);

        Assert.False(Directory.Exists(folder));
    }

    [Fact]
    public void MissingFolder_DoesNothing()
    {
        FileSystemDeletion.DeleteFolder(Path.Combine(_root, "missing"));
    }

    [Fact]
    public void LinkInside_DeletesLinkButNotTarget()
    {
        string target = Path.Combine(_root, "target");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "keep.txt"), "keep");
        string folder = Path.Combine(_root, "folder");
        Directory.CreateDirectory(folder);
        if (!TryCreateLink(Path.Combine(folder, "link"), target))
        {
            Assert.Skip("Symbolic links can't be created here.");
        }

        FileSystemDeletion.DeleteFolder(folder);

        Assert.False(Directory.Exists(folder));
        Assert.True(File.Exists(Path.Combine(target, "keep.txt")));
    }

    [Fact]
    public void FolderIsLink_DeletesOnlyLink()
    {
        string target = Path.Combine(_root, "target");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "keep.txt"), "keep");
        string link = Path.Combine(_root, "link");
        if (!TryCreateLink(link, target))
        {
            Assert.Skip("Symbolic links can't be created here.");
        }

        FileSystemDeletion.DeleteFolder(link);

        Assert.False(Path.Exists(link));
        Assert.True(File.Exists(Path.Combine(target, "keep.txt")));
    }

    private static bool TryCreateLink(string path, string target)
    {
        try
        {
            Directory.CreateSymbolicLink(path, target);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Windows needs Developer Mode or elevation to create symbolic links.
            return false;
        }
    }
}
