using Deosrc.Tammi.Core.Tools;

namespace Deosrc.Tammi.Core.Tests.Tools;

public sealed class SolutionFinderTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void Find_ReturnsSlnAndSlnxFiles()
    {
        var sln = _folder.AddFile("Api.sln");
        var slnx = _folder.AddFile("Web.slnx");
        _folder.AddFile("README.md");
        _folder.AddFile("Api.csproj");

        Assert.Equal([sln, slnx], SolutionFinder.Find(_folder.Path));
    }

    [Fact]
    public void Find_MatchesExtensionsInAnyCase()
    {
        var solution = _folder.AddFile("Api.SLN");

        Assert.Equal([solution], SolutionFinder.Find(_folder.Path));
    }

    [Fact]
    public void Find_SearchesSubfolders_OrderedByPath()
    {
        var nested = _folder.AddFile(Path.Combine("src", "Api", "Api.sln"));
        var root = _folder.AddFile("All.sln");
        var tools = _folder.AddFile(Path.Combine("tools", "Tools.slnx"));

        Assert.Equal([root, nested, tools], SolutionFinder.Find(_folder.Path));
    }

    [Fact]
    public void Find_StopsAtMaxDepth()
    {
        var shallow = _folder.AddFile(Path.Combine("a", "b", "c", "Deep.sln"));
        _folder.AddFile(Path.Combine("a", "b", "c", "d", "TooDeep.sln"));

        Assert.Equal([shallow], SolutionFinder.Find(_folder.Path));
        Assert.Empty(SolutionFinder.Find(_folder.Path, maxDepth: 2));
    }

    [Fact]
    public void Find_WithMaxDepthZero_OnlySearchesFolderItself()
    {
        var root = _folder.AddFile("Api.sln");
        _folder.AddFile(Path.Combine("src", "Web.sln"));

        Assert.Equal([root], SolutionFinder.Find(_folder.Path, maxDepth: 0));
    }

    [Theory]
    [InlineData(".git")]
    [InlineData(".vs")]
    [InlineData("bin")]
    [InlineData("obj")]
    [InlineData("node_modules")]
    [InlineData("packages")]
    [InlineData("BIN")]
    public void Find_SkipsHiddenAndBuildFolders(string skipped)
    {
        _folder.AddFile(Path.Combine(skipped, "Skipped.sln"));
        var kept = _folder.AddFile(Path.Combine("src", "Kept.sln"));

        Assert.Equal([kept], SolutionFinder.Find(_folder.Path));
    }

    [Fact]
    public void Find_DoesNotFollowLinkedFolders()
    {
        var solution = _folder.AddFile(Path.Combine("src", "Api.sln"));
        try
        {
            Directory.CreateSymbolicLink(Path.Combine(_folder.Path, "link"), Path.Combine(_folder.Path, "src"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Windows only lets administrators and developer mode create links.
            Assert.Skip("Can't create a symbolic link here.");
        }

        Assert.Equal([solution], SolutionFinder.Find(_folder.Path));
    }

    [Fact]
    public void Find_WithMissingFolder_IsEmpty()
    {
        Assert.Empty(SolutionFinder.Find(Path.Combine(_folder.Path, "missing")));
    }

    [Fact]
    public void Find_WithNoSolutions_IsEmpty()
    {
        _folder.AddFile(Path.Combine("src", "Program.cs"));

        Assert.Empty(SolutionFinder.Find(_folder.Path));
    }
}
