using Tammi.Core.Platform;

namespace Tammi.Core.Tests.Platform;

public class ExecutableResolverTests
{
    private static readonly string Bin = Rooted("bin");
    private static readonly string Tools = Rooted("tools");
    private static readonly string SearchPath = string.Join(Path.PathSeparator, Bin, Tools);

    [Fact]
    public void Resolve_FindsNameInFirstDirectoryThatHasIt()
    {
        var resolver = Resolver(SearchPath, [""], Path.Combine(Bin, "git"), Path.Combine(Tools, "git"));

        Assert.Equal(Path.Combine(Bin, "git"), resolver.Resolve("git"));
    }

    [Fact]
    public void Resolve_TriesEachExtensionInOrder()
    {
        var resolver = Resolver(
            SearchPath,
            [".COM", ".EXE", ".CMD"],
            Path.Combine(Tools, "code.CMD"),
            Path.Combine(Tools, "code.EXE"));

        Assert.Equal(Path.Combine(Tools, "code.EXE"), resolver.Resolve("code"));
    }

    [Fact]
    public void Resolve_PrefersEarlierDirectoryOverEarlierExtension()
    {
        var resolver = Resolver(
            SearchPath,
            [".EXE", ".CMD"],
            Path.Combine(Bin, "code.CMD"),
            Path.Combine(Tools, "code.EXE"));

        Assert.Equal(Path.Combine(Bin, "code.CMD"), resolver.Resolve("code"));
    }

    [Fact]
    public void Resolve_TriesNameAsItIs_WhenItHasAKnownExtension()
    {
        var resolver = Resolver(SearchPath, [".EXE", ".CMD"], Path.Combine(Tools, "code.cmd"));

        Assert.Equal(Path.Combine(Tools, "code.cmd"), resolver.Resolve("code.cmd"));
    }

    [Fact]
    public void Resolve_DoesNotTryNameAsItIs_WhenItsExtensionIsUnknown()
    {
        var resolver = Resolver(SearchPath, [".EXE"], Path.Combine(Tools, "script.sh"));

        Assert.Null(resolver.Resolve("script.sh"));
    }

    [Fact]
    public void Resolve_ChecksPathsWithoutSearching()
    {
        var path = Path.Combine(Rooted("elsewhere"), "git");
        var resolver = Resolver(SearchPath, [""], path, Path.Combine(Bin, "git"));

        Assert.Equal(path, resolver.Resolve(path));
        Assert.Null(resolver.Resolve(Path.Combine(Rooted("missing"), "git")));
    }

    [Fact]
    public void Resolve_IgnoresEmptyQuotedAndRelativeSearchPathEntries()
    {
        var searchPath = string.Join(Path.PathSeparator, "", "relative", $"\"{Tools}\"", " ");
        var resolver = Resolver(searchPath, [""], Path.Combine("relative", "git"), Path.Combine(Tools, "git"));

        Assert.Equal(Path.Combine(Tools, "git"), resolver.Resolve("git"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Resolve_ReturnsNull_WhenThereIsNoSearchPath(string? searchPath)
    {
        var resolver = Resolver(searchPath, [""], Path.Combine(Bin, "git"));

        Assert.Null(resolver.Resolve("git"));
    }

    [Fact]
    public void Resolve_ReturnsNull_WhenNotFound()
    {
        var resolver = Resolver(SearchPath, [""]);

        Assert.Null(resolver.Resolve("git"));
    }

    private static ExecutableResolver Resolver(string? searchPath, string[] extensions, params string[] executables) =>
        new(searchPath, extensions, executables.Contains);

    private static string Rooted(string name) => Path.Combine(Path.GetPathRoot(Path.GetTempPath())!, name);
}
