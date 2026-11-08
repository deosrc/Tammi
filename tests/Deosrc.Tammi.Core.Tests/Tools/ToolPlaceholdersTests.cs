using Deosrc.Tammi.Core.Tools;

namespace Deosrc.Tammi.Core.Tests.Tools;

public class ToolPlaceholdersTests
{
    private static readonly Dictionary<string, string> Values = new()
    {
        [ToolPlaceholders.LaunchFolder] = "/work/acme/api",
        [ToolPlaceholders.FilePath] = "/work/acme/api/Api.sln",
        [ToolPlaceholders.FileNameWithoutExtension] = "Api",
    };

    [Fact]
    public void Expand_ReplacesPlaceholder()
    {
        Assert.Equal("/work/acme/api", ToolPlaceholders.Expand("{LaunchFolder}", Values));
    }

    [Fact]
    public void Expand_ReplacesPlaceholdersWithinText()
    {
        Assert.Equal(
            "Visual Studio - Api (/work/acme/api/Api.sln)",
            ToolPlaceholders.Expand("Visual Studio - {FileNameWithoutExtension} ({FilePath})", Values));
    }

    [Fact]
    public void Expand_ReplacesEveryUse()
    {
        Assert.Equal("Api-Api", ToolPlaceholders.Expand("{FileNameWithoutExtension}-{FileNameWithoutExtension}", Values));
    }

    [Theory]
    [InlineData("{launchfolder}")]
    [InlineData("{LAUNCHFOLDER}")]
    public void Expand_IgnoresCase(string text)
    {
        Assert.Equal("/work/acme/api", ToolPlaceholders.Expand(text, Values));
    }

    [Fact]
    public void Expand_KeepsValueWithSpacesAsItIs()
    {
        var values = new Dictionary<string, string> { [ToolPlaceholders.LaunchFolder] = @"C:\My Work\acme api" };

        Assert.Equal(@"C:\My Work\acme api", ToolPlaceholders.Expand("{LaunchFolder}", values));
    }

    [Fact]
    public void Expand_DoesNotExpandPlaceholdersInValues()
    {
        var values = new Dictionary<string, string>
        {
            [ToolPlaceholders.LaunchFolder] = "/work/{FilePath}",
            [ToolPlaceholders.FilePath] = "/file",
        };

        Assert.Equal("/work/{FilePath}", ToolPlaceholders.Expand("{LaunchFolder}", values));
    }

    [Fact]
    public void Expand_LeavesUnknownTextInBracesAsItIs()
    {
        Assert.Equal("{path} --x={y}", ToolPlaceholders.Expand("{path} --x={y}", Values));
    }

    [Fact]
    public void Expand_WithoutPlaceholders_ReturnsText()
    {
        Assert.Equal("--new-window", ToolPlaceholders.Expand("--new-window", Values));
    }

    [Fact]
    public void Expand_WithPlaceholderThatHasNoValue_IsNull()
    {
        Assert.Null(ToolPlaceholders.Expand("{LaunchFolder} {RepositoryFolder}", Values));
    }

    [Fact]
    public void FindUsed_ReturnsKnownPlaceholdersOnce_AsWritten()
    {
        Assert.Equal(
            [ToolPlaceholders.FilePath, ToolPlaceholders.LaunchFolder],
            ToolPlaceholders.FindUsed("{filepath} {LaunchFolder} {FilePath} {other}"));
    }

    [Fact]
    public void FindUnknown_ReturnsOtherTextInBraces()
    {
        Assert.Equal(["path", "Other"], ToolPlaceholders.FindUnknown("{path} {LaunchFolder} {Other} {path}"));
    }

    [Fact]
    public void For_FolderTool_HasNoFilePlaceholders()
    {
        Assert.Equal(
            [ToolPlaceholders.LaunchFolder, ToolPlaceholders.WorkspaceFolder, ToolPlaceholders.RepositoryFolder],
            ToolPlaceholders.For(ToolKind.Folder));
        Assert.Equal(ToolPlaceholders.All, ToolPlaceholders.For(ToolKind.File));
    }
}
