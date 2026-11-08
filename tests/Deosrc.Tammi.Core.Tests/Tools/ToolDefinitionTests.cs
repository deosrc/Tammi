using Deosrc.Tammi.Core.Tools;

namespace Deosrc.Tammi.Core.Tests.Tools;

public class ToolDefinitionTests
{
    [Fact]
    public void BuildArguments_ReplacesPathPlaceholder()
    {
        var tool = new ToolDefinition { Arguments = ["--new-window", ToolDefinition.PathPlaceholder] };

        Assert.Equal(["--new-window", "/work/acme"], tool.BuildArguments("/work/acme"));
    }

    [Fact]
    public void BuildArguments_ReplacesPlaceholderWithinArgument()
    {
        var tool = new ToolDefinition { Arguments = ["--folder={path}", "--title", "Tammi: {path}"] };

        Assert.Equal(["--folder=/work/acme", "--title", "Tammi: /work/acme"], tool.BuildArguments("/work/acme"));
    }

    [Fact]
    public void BuildArguments_KeepsPathWithSpacesAsOneArgument()
    {
        var tool = new ToolDefinition();

        Assert.Equal([@"C:\My Work\acme api"], tool.BuildArguments(@"C:\My Work\acme api"));
    }

    [Fact]
    public void BuildArguments_DoesNotExpandPlaceholdersInThePath()
    {
        var tool = new ToolDefinition { Arguments = ["{path}", "{path}"] };

        Assert.Equal(["/work/{path}", "/work/{path}"], tool.BuildArguments("/work/{path}"));
    }

    [Fact]
    public void BuildArguments_WithoutPlaceholder_DoesNotAddPath()
    {
        var tool = new ToolDefinition { Arguments = ["--reuse-window"] };

        Assert.False(tool.HasPathPlaceholder);
        Assert.Equal(["--reuse-window"], tool.BuildArguments("/work/acme"));
    }

    [Fact]
    public void BuildArguments_IsCaseSensitive()
    {
        var tool = new ToolDefinition { Arguments = ["{PATH}"] };

        Assert.False(tool.HasPathPlaceholder);
        Assert.Equal(["{PATH}"], tool.BuildArguments("/work/acme"));
    }

    [Fact]
    public void NewTool_OpensFolderAsItsOnlyArgument()
    {
        var tool = new ToolDefinition();

        Assert.True(tool.HasPathPlaceholder);
        Assert.True(tool.Enabled);
        Assert.Equal(ToolScope.Both, tool.Scope);
        Assert.Equal(["/work/acme"], tool.BuildArguments("/work/acme"));
    }

    [Fact]
    public void Normalize_FillsInMissingValuesAndTrimsExecutable()
    {
        var tool = new ToolDefinition
        {
            Name = null!,
            Executable = "  code ",
            Arguments = ["{path}", null!],
            Icon = (ToolIcon)99,
            Scope = (ToolScope)99,
        }.Normalize();

        Assert.Equal(
            new ToolDefinition
            {
                Name = "",
                Executable = "code",
                Arguments = ["{path}"],
                Icon = ToolIcon.Application,
                Scope = ToolScope.Both,
            },
            tool);
    }

    [Fact]
    public void Normalize_WithoutArguments_HasNone()
    {
        Assert.Empty(new ToolDefinition { Arguments = null! }.Normalize().Arguments);
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

    [Fact]
    public void Equals_ComparesArguments()
    {
        Assert.Equal(new ToolDefinition { Arguments = ["a", "{path}"] }, new ToolDefinition { Arguments = ["a", "{path}"] });
        Assert.NotEqual(new ToolDefinition { Arguments = ["a"] }, new ToolDefinition { Arguments = ["b"] });
    }
}
