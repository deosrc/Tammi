using Deosrc.Tammi.Core.Tools;

namespace Deosrc.Tammi.Core.Tests.Tools;

public sealed class ToolFileFinderTests : IDisposable
{
    private readonly TempFolder _folder = new();

    private static readonly string[] Solutions = ["*.sln", "*.slnx"];

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void Find_ReturnsFilesMatchingAnyPattern()
    {
        var sln = _folder.AddFile("Api.sln");
        var slnx = _folder.AddFile("Web.slnx");
        _folder.AddFile("README.md");
        _folder.AddFile("Api.csproj");

        Assert.Equal([sln, slnx], ToolFileFinder.Find(_folder.Path, Solutions));
    }

    [Fact]
    public void Find_MatchesExtensionsInAnyCase()
    {
        var solution = _folder.AddFile("Api.SLN");

        Assert.Equal([solution], ToolFileFinder.Find(_folder.Path, Solutions));
    }

    [Fact]
    public void Find_SearchesSubfolders_OrderedByPath()
    {
        var nested = _folder.AddFile(Path.Combine("src", "Api", "Api.sln"));
        var root = _folder.AddFile("All.sln");
        var tools = _folder.AddFile(Path.Combine("tools", "Tools.slnx"));

        Assert.Equal([root, nested, tools], ToolFileFinder.Find(_folder.Path, Solutions));
    }

    [Fact]
    public void Find_StopsAtMaxDepth()
    {
        var shallow = _folder.AddFile(Path.Combine("a", "b", "c", "Deep.sln"));
        _folder.AddFile(Path.Combine("a", "b", "c", "d", "TooDeep.sln"));

        Assert.Equal([shallow], ToolFileFinder.Find(_folder.Path, Solutions));
        Assert.Empty(ToolFileFinder.Find(_folder.Path, Solutions, maxDepth: 2));
    }

    [Fact]
    public void Find_WithMaxDepthZero_OnlySearchesFolderItself()
    {
        var root = _folder.AddFile("Api.sln");
        _folder.AddFile(Path.Combine("src", "Web.sln"));

        Assert.Equal([root], ToolFileFinder.Find(_folder.Path, Solutions, maxDepth: 0));
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

        Assert.Equal([kept], ToolFileFinder.Find(_folder.Path, Solutions));
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

        Assert.Equal([solution], ToolFileFinder.Find(_folder.Path, Solutions));
    }

    [Fact]
    public void Find_WithMissingFolder_IsEmpty()
    {
        Assert.Empty(ToolFileFinder.Find(Path.Combine(_folder.Path, "missing"), Solutions));
    }

    [Fact]
    public void Find_WithNoSolutions_IsEmpty()
    {
        _folder.AddFile(Path.Combine("src", "Program.cs"));

        Assert.Empty(ToolFileFinder.Find(_folder.Path, Solutions));
    }

    [Fact]
    public void Find_SupportsQuestionMarkWildcard()
    {
        var match = _folder.AddFile("v1.json");
        _folder.AddFile("v10.json");

        Assert.Equal([match], ToolFileFinder.Find(_folder.Path, ["v?.json"]));
    }

    [Fact]
    public void Find_WithPathPattern_OnlyLooksThere_IncludingHiddenFolders()
    {
        var config = _folder.AddFile(Path.Combine(".devcontainer", "devcontainer.json"));
        _folder.AddFile(Path.Combine("src", ".devcontainer", "devcontainer.json"));
        _folder.AddFile("devcontainer.json");

        Assert.Equal([config], ToolFileFinder.Find(_folder.Path, [".devcontainer/devcontainer.json"]));
    }

    [Fact]
    public void Find_WithPathPattern_SupportsWildcardsAndBackslashes()
    {
        var python = _folder.AddFile(Path.Combine(".devcontainer", "python", "devcontainer.json"));
        var node = _folder.AddFile(Path.Combine(".devcontainer", "node", "devcontainer.json"));

        Assert.Equal(
            [node, python],
            ToolFileFinder.Find(_folder.Path, [@".devcontainer\*\devcontainer.json"]));
    }

    [Fact]
    public void Find_WithLeadingSlash_OnlyMatchesInFolderItself()
    {
        var root = _folder.AddFile("global.json");
        _folder.AddFile(Path.Combine("src", "global.json"));

        Assert.Equal([root], ToolFileFinder.Find(_folder.Path, ["/global.json"]));
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("./inside.txt")]
    public void Find_WithRelativeSegments_FindsNothing(string pattern)
    {
        _folder.AddFile("inside.txt");

        Assert.Empty(ToolFileFinder.Find(_folder.Path, [pattern]));
    }

    [Fact]
    public void Find_WithOverlappingPatterns_ReturnsEachFileOnce()
    {
        var solution = _folder.AddFile("Api.sln");

        Assert.Equal([solution], ToolFileFinder.Find(_folder.Path, ["*.sln", "Api.*", "/Api.sln", " "]));
    }

    [Fact]
    public void Find_WithoutPatterns_IsEmpty()
    {
        _folder.AddFile("Api.sln");

        Assert.Empty(ToolFileFinder.Find(_folder.Path, []));
    }
}
