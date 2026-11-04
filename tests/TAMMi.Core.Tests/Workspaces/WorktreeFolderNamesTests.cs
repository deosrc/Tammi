using TAMMi.Core.Workspaces;

namespace TAMMi.Core.Tests.Workspaces;

public sealed class WorktreeFolderNamesTests
{
    [Fact]
    public void Suggest_UsesRepositoryNames()
    {
        var names = WorktreeFolderNames.Suggest([("api", "acme", null), ("web", "acme", null)]);

        Assert.Equal(["api", "web"], names);
    }

    [Fact]
    public void Suggest_SameName_PrefixesOwners()
    {
        var names = WorktreeFolderNames.Suggest([("api", "acme", null), ("web", "acme", null), ("api", "other", null)]);

        Assert.Equal(["acme-api", "web", "other-api"], names);
    }

    [Fact]
    public void Suggest_NamesDifferingOnlyInCase_Clash()
    {
        var names = WorktreeFolderNames.Suggest([("API", "acme", null), ("api", "other", null)]);

        Assert.Equal(["acme-API", "other-api"], names);
    }

    [Fact]
    public void Suggest_SameNameAndOwner_AddsNumber()
    {
        var names = WorktreeFolderNames.Suggest([("api", "acme", null), ("api", "acme", null), ("api", "acme", null)]);

        Assert.Equal(["acme-api", "acme-api-2", "acme-api-3"], names);
    }

    [Fact]
    public void Suggest_NoOwner_AddsNumber()
    {
        var names = WorktreeFolderNames.Suggest([("api", null, null), ("api", "", null)]);

        Assert.Equal(["api", "api-2"], names);
    }

    [Fact]
    public void Suggest_KeepsChosenNames()
    {
        var names = WorktreeFolderNames.Suggest([("api", "acme", "server"), ("web", "acme", null)]);

        Assert.Equal(["server", "web"], names);
    }

    [Fact]
    public void Suggest_ClashWithChosenName_PrefixesOwner()
    {
        var names = WorktreeFolderNames.Suggest([("web", "acme", "api"), ("api", "acme", null)]);

        Assert.Equal(["api", "acme-api"], names);
    }

    [Fact]
    public void Suggest_ClashWithChosenOwnerPrefixedName_AddsNumber()
    {
        var names = WorktreeFolderNames.Suggest([("web", "acme", "acme-api"), ("api", "acme", "api"), ("api", "acme", null)]);

        Assert.Equal(["acme-api", "api", "acme-api-2"], names);
    }

    [Fact]
    public void Suggest_RepositoryNamedLikeMetadataFolder_PrefixesOwner()
    {
        var names = WorktreeFolderNames.Suggest([(".tammi", "acme", null)]);

        Assert.Equal(["acme-.tammi"], names);
    }

    [Theory]
    [InlineData("api")]
    [InlineData("acme-api")]
    [InlineData(" api ")]
    public void Validate_ValidName_HasNoProblem(string name)
    {
        Assert.Null(WorktreeFolderNames.Validate(name, ["web"]));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("../other")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("api.")]
    [InlineData("CON")]
    [InlineData(".tammi")]
    [InlineData(".TAMMI")]
    public void Validate_InvalidName_HasProblem(string name)
    {
        Assert.NotNull(WorktreeFolderNames.Validate(name, []));
    }

    [Fact]
    public void Validate_TooLong_HasProblem()
    {
        Assert.NotNull(WorktreeFolderNames.Validate(new string('a', 256), []));
    }

    [Theory]
    [InlineData("api")]
    [InlineData("API")]
    [InlineData(" api")]
    public void Validate_SameAsAnotherRepository_HasProblem(string name)
    {
        Assert.NotNull(WorktreeFolderNames.Validate(name, ["web", "api "]));
    }
}
