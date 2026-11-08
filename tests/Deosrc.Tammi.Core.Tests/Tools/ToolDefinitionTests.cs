using Deosrc.Tammi.Core.Tools;

namespace Deosrc.Tammi.Core.Tests.Tools;

public class ToolDefinitionTests
{
    [Fact]
    public void Normalize_FillsInMissingValuesAndTrims()
    {
        var tool = new ToolDefinition
        {
            Kind = (ToolKind)99,
            Name = null!,
            FilePatterns = [" *.sln ", "", null!],
            Executable = "  code ",
            Arguments = ["{LaunchFolder}", null!],
            Scope = (ToolScope)99,
            Icon = null!,
        }.Normalize();

        Assert.Equal(
            new ToolDefinition
            {
                Name = "",
                FilePatterns = ["*.sln"],
                Executable = "code",
                Arguments = ["{LaunchFolder}"],
            },
            tool);
    }

    [Fact]
    public void Normalize_ReplacesUnknownIconValues()
    {
        var icon = new ToolDefinition
        {
            Icon = new ToolIconSettings { Source = (ToolIconSource)9, Generic = (ToolIcon)9, CustomPath = "  " },
        }.Normalize().Icon;

        Assert.Equal(new ToolIconSettings(), icon);
    }

    [Theory]
    [InlineData(ToolScope.Both, false, true)]
    [InlineData(ToolScope.Both, true, true)]
    [InlineData(ToolScope.Workspace, false, true)]
    [InlineData(ToolScope.Workspace, true, false)]
    [InlineData(ToolScope.Repository, false, false)]
    [InlineData(ToolScope.Repository, true, true)]
    public void Includes_MatchesScope(ToolScope scope, bool repository, bool expected)
    {
        Assert.Equal(expected, ToolDefinition.Includes(scope, repository));
    }

    [Theory]
    [InlineData(ToolScope.Both, false, true)]
    [InlineData(ToolScope.Workspace, true, false)]
    [InlineData(ToolScope.Repository, false, true)]
    public void IsOfferedFor_FolderToolFollowsScope(ToolScope scope, bool workspace, bool repository)
    {
        var tool = new ToolDefinition { Scope = scope };

        Assert.Equal(workspace || scope == ToolScope.Both, tool.IsOfferedFor(repository: false));
        Assert.Equal(repository, tool.IsOfferedFor(repository: true));
    }

    [Theory]
    [InlineData(ToolScope.Workspace)]
    [InlineData(ToolScope.Repository)]
    public void IsOfferedFor_FileToolIsOfferedForBoth(ToolScope scope)
    {
        var tool = new ToolDefinition { Kind = ToolKind.File, Scope = scope };

        Assert.True(tool.IsOfferedFor(repository: false));
        Assert.True(tool.IsOfferedFor(repository: true));
    }

    [Fact]
    public void Equals_ComparesLists()
    {
        Assert.Equal(
            new ToolDefinition { Arguments = ["a"], FilePatterns = ["*.sln"] },
            new ToolDefinition { Arguments = ["a"], FilePatterns = ["*.sln"] });
        Assert.NotEqual(new ToolDefinition { Arguments = ["a"] }, new ToolDefinition { Arguments = ["b"] });
        Assert.NotEqual(new ToolDefinition { FilePatterns = ["a"] }, new ToolDefinition { FilePatterns = ["b"] });
    }

    [Fact]
    public void PredefinedTools_HaveUniqueIdsAndOnlyAvailablePlaceholders()
    {
        Assert.Equal(PredefinedTools.All.Count, PredefinedTools.All.Select(tool => tool.Id).Distinct().Count());
        Assert.All(PredefinedTools.All, tool =>
        {
            Assert.True(tool.IsPredefined);
            Assert.All(
                tool.Arguments.Append(tool.Name).SelectMany(ToolPlaceholders.FindUsed),
                placeholder => Assert.Contains(placeholder, ToolPlaceholders.For(tool.Kind)));
            Assert.Empty(tool.Arguments.Append(tool.Name).SelectMany(ToolPlaceholders.FindUnknown));
            Assert.Equal(tool.Kind == ToolKind.File, tool.FilePatterns.Count > 0);
        });
    }

    [Fact]
    public void VisualStudio_IsOnlyPredefinedOnWindows()
    {
        Assert.Equal(OperatingSystem.IsWindows(), PredefinedTools.All.Contains(PredefinedTools.VisualStudio));
    }
}
