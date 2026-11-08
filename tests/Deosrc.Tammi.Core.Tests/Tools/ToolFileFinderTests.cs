using Deosrc.Tammi.Core.Tools;

namespace Deosrc.Tammi.Core.Tests.Tools;

public sealed class ToolFileFinderTests : IDisposable
{
    private readonly TempFolder _folder = new();

    private static readonly string[] Solutions = ["*.sln", "*.slnx"];

    public void Dispose() => _folder.Dispose();

    // Folder names for a path that many folders deep, e.g. ["d1", "d2", "d3"].
    private static string[] Folders(int depth) => Enumerable.Range(1, depth).Select(i => $"d{i}").ToArray();

    private static IReadOnlyList<string> Files(string folder, IEnumerable<string> patterns, int maxDepth = ToolFileFinder.DefaultMaxDepth) =>
        ToolFileFinder.Find(folder, patterns, maxDepth).Select(match => match.FilePath).ToArray();

    [Fact]
    public void Find_ReturnsFilesMatchingAnyPattern()
    {
        var sln = _folder.AddFile("Api.sln");
        var slnx = _folder.AddFile("Web.slnx");
        _folder.AddFile("README.md");
        _folder.AddFile("Api.csproj");

        Assert.Equal([sln, slnx], Files(_folder.Path, Solutions));
    }

    [Fact]
    public void Find_MatchesExtensionsInAnyCase()
    {
        var solution = _folder.AddFile("Api.SLN");

        Assert.Equal([solution], Files(_folder.Path, Solutions));
    }

    [Fact]
    public void Find_SearchesSubfolders_OrderedByPath()
    {
        var nested = _folder.AddFile(Path.Combine("src", "Api", "Api.sln"));
        var root = _folder.AddFile("All.sln");
        var tools = _folder.AddFile(Path.Combine("tools", "Tools.slnx"));

        Assert.Equal([root, nested, tools], Files(_folder.Path, Solutions));
    }

    [Fact]
    public void Find_StopsAtMaxDepth()
    {
        var deepest = _folder.AddFile(Path.Combine([.. Folders(ToolFileFinder.DefaultMaxDepth), "Deep.sln"]));
        _folder.AddFile(Path.Combine([.. Folders(ToolFileFinder.DefaultMaxDepth + 1), "TooDeep.sln"]));

        Assert.Equal([deepest], Files(_folder.Path, Solutions));
        Assert.Empty(Files(_folder.Path, Solutions, maxDepth: ToolFileFinder.DefaultMaxDepth - 1));
    }

    [Fact]
    public void Find_WithMaxDepthZero_OnlySearchesFolderItself()
    {
        var root = _folder.AddFile("Api.sln");
        _folder.AddFile(Path.Combine("src", "Web.sln"));

        Assert.Equal([root], Files(_folder.Path, Solutions, maxDepth: 0));
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

        Assert.Equal([kept], Files(_folder.Path, Solutions));
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

        Assert.Equal([solution], Files(_folder.Path, Solutions));
    }

    [Fact]
    public void Find_WithMissingFolder_IsEmpty()
    {
        Assert.Empty(Files(Path.Combine(_folder.Path, "missing"), Solutions));
    }

    [Fact]
    public void Find_WithNoSolutions_IsEmpty()
    {
        _folder.AddFile(Path.Combine("src", "Program.cs"));

        Assert.Empty(Files(_folder.Path, Solutions));
    }

    [Fact]
    public void Find_SupportsQuestionMarkWildcard()
    {
        var match = _folder.AddFile("v1.json");
        _folder.AddFile("v10.json");

        Assert.Equal([match], Files(_folder.Path, ["v?.json"]));
    }

    [Fact]
    public void Find_WithPathPattern_MatchesInEverySearchedFolder_IncludingHiddenFoldersItNames()
    {
        var root = _folder.AddFile(Path.Combine(".devcontainer", "devcontainer.json"));
        var nested = _folder.AddFile(Path.Combine("services", "api", ".devcontainer", "devcontainer.json"));
        _folder.AddFile("devcontainer.json");

        var matches = ToolFileFinder.Find(_folder.Path, [".devcontainer/devcontainer.json"]);

        Assert.Equal(
            [
                new ToolFileMatch(root, _folder.Path),
                new ToolFileMatch(nested, Path.Combine(_folder.Path, "services", "api")),
            ],
            matches);
    }

    [Fact]
    public void Find_WithPathPattern_SkipsSearchedFoldersLikeFileNames()
    {
        _folder.AddFile(Path.Combine("node_modules", "pkg", ".devcontainer", "devcontainer.json"));
        _folder.AddFile(Path.Combine(".git", "x", ".devcontainer", "devcontainer.json"));
        _folder.AddFile(Path.Combine(
            [.. Folders(ToolFileFinder.DefaultMaxDepth + 1), ".devcontainer", "devcontainer.json"]));
        var deepest = _folder.AddFile(Path.Combine(
            [.. Folders(ToolFileFinder.DefaultMaxDepth), ".devcontainer", "devcontainer.json"]));

        Assert.Equal([deepest], Files(_folder.Path, [".devcontainer/devcontainer.json"]));
    }

    [Fact]
    public void Find_WithFileName_MatchFolderIsTheFilesFolder()
    {
        var file = _folder.AddFile(Path.Combine("src", ".devcontainer.json"));

        Assert.Equal(
            new ToolFileMatch(file, Path.Combine(_folder.Path, "src")),
            Assert.Single(ToolFileFinder.Find(_folder.Path, [".devcontainer.json"])));
    }

    [Fact]
    public void Find_WithFileMatchingSeveralPatterns_TakesTheFirstPatternsMatchFolder()
    {
        var file = _folder.AddFile(Path.Combine("src", "config", "app.json"));

        Assert.Equal(
            Path.Combine(_folder.Path, "src"),
            Assert.Single(ToolFileFinder.Find(_folder.Path, ["config/app.json", "app.json"])).MatchFolder);
        Assert.Equal(
            Path.GetDirectoryName(file),
            Assert.Single(ToolFileFinder.Find(_folder.Path, ["app.json", "config/app.json"])).MatchFolder);
    }

    [Fact]
    public void Find_WithPathPattern_SupportsWildcardsAndBackslashes()
    {
        var python = _folder.AddFile(Path.Combine(".devcontainer", "python", "devcontainer.json"));
        var node = _folder.AddFile(Path.Combine(".devcontainer", "node", "devcontainer.json"));

        Assert.Equal(
            [node, python],
            Files(_folder.Path, [@".devcontainer\*\devcontainer.json"]));
    }

    [Fact]
    public void Find_WithLeadingSlash_OnlyMatchesInFolderItself()
    {
        var root = _folder.AddFile("global.json");
        _folder.AddFile(Path.Combine("src", "global.json"));

        Assert.Equal([root], Files(_folder.Path, ["/global.json"]));
        Assert.Equal(_folder.Path, Assert.Single(ToolFileFinder.Find(_folder.Path, ["/global.json"])).MatchFolder);
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("./inside.txt")]
    public void Find_WithRelativeSegments_FindsNothing(string pattern)
    {
        _folder.AddFile("inside.txt");

        Assert.Empty(Files(_folder.Path, [pattern]));
    }

    [Fact]
    public void Find_WithOverlappingPatterns_ReturnsEachFileOnce()
    {
        var solution = _folder.AddFile("Api.sln");

        Assert.Equal([solution], Files(_folder.Path, ["*.sln", "Api.*", "/Api.sln", " "]));
    }

    [Fact]
    public void Find_WithoutPatterns_IsEmpty()
    {
        _folder.AddFile("Api.sln");

        Assert.Empty(Files(_folder.Path, []));
    }
}
