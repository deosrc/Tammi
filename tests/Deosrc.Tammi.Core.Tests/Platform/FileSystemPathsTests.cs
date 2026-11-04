using Deosrc.Tammi.Core.Platform;

namespace Deosrc.Tammi.Core.Tests.Platform;

public sealed class FileSystemPathsTests
{
    private static readonly string Root = Path.GetPathRoot(Path.GetTempPath())!;

    private static string Combine(params string[] parts) => Path.Combine([Root, .. parts]);

    [Fact]
    public void IsSameOrInside_ForTheFolderAndItsSubfolders_IsTrue()
    {
        string folder = Combine("repos");

        Assert.True(FileSystemPaths.IsSameOrInside(folder, folder));
        Assert.True(FileSystemPaths.IsSameOrInside(folder + Path.DirectorySeparatorChar, folder));
        Assert.True(FileSystemPaths.IsSameOrInside(Combine("repos", "api"), folder));
        Assert.True(FileSystemPaths.IsSameOrInside(Combine("repos", "acme", "web"), folder + Path.DirectorySeparatorChar));
        Assert.True(FileSystemPaths.IsSameOrInside(Combine("repos", "x", "..", "api"), folder));
    }

    [Fact]
    public void IsSameOrInside_ForOtherFolders_IsFalse()
    {
        string folder = Combine("repos");

        Assert.False(FileSystemPaths.IsSameOrInside(Combine("repos-old", "api"), folder));
        Assert.False(FileSystemPaths.IsSameOrInside(Combine("other"), folder));
        Assert.False(FileSystemPaths.IsSameOrInside(Root, folder));
    }

    [Fact]
    public void IsSameOrInside_ForTheRoot_IncludesEverything()
    {
        Assert.True(FileSystemPaths.IsSameOrInside(Combine("repos", "api"), Root));
    }

    [Fact]
    public void IsSameOrInside_ComparesCaseAsTheFileSystemDoes()
    {
        Assert.Equal(
            OperatingSystem.IsWindows(),
            FileSystemPaths.IsSameOrInside(Combine("REPOS", "api"), Combine("repos")));
    }
}
