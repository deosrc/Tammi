using System.Text;
using Deosrc.Tammi.Core.Tools;

namespace Deosrc.Tammi.Core.Tests.Tools;

public sealed class DevContainerConfigTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    private string FolderName => Path.GetFileName(_folder.Path);

    [Fact]
    public void Find_WithoutConfig_IsNull()
    {
        Assert.Null(DevContainerConfig.Find(_folder.Path));
    }

    [Fact]
    public void Find_InDevContainerFolder_DefaultsToWorkspacesFolder()
    {
        var path = _folder.AddFile(Path.Combine(".devcontainer", "devcontainer.json"), "{}");

        Assert.Equal(new DevContainerConfig(path, "/workspaces/" + FolderName), DevContainerConfig.Find(_folder.Path));
    }

    [Fact]
    public void Find_InFolder_IsFound()
    {
        var path = _folder.AddFile(".devcontainer.json", "{}");

        Assert.Equal(path, DevContainerConfig.Find(_folder.Path)?.FilePath);
    }

    [Fact]
    public void Find_WithBoth_PrefersDevContainerFolder()
    {
        var preferred = _folder.AddFile(Path.Combine(".devcontainer", "devcontainer.json"), "{}");
        _folder.AddFile(".devcontainer.json", "{}");

        Assert.Equal(preferred, DevContainerConfig.Find(_folder.Path)?.FilePath);
    }

    [Fact]
    public void Find_OnlyInSubfolder_IsNull()
    {
        _folder.AddFile(Path.Combine(".devcontainer", "python", "devcontainer.json"), "{}");

        Assert.Null(DevContainerConfig.Find(_folder.Path));
    }

    [Fact]
    public void Find_ReadsWorkspaceFolder_WithCommentsAndTrailingCommas()
    {
        _folder.AddFile(".devcontainer.json", """
            {
                // Where the source is mounted.
                "workspaceFolder": "/src/${localWorkspaceFolderBasename}",
                "image": "mcr.microsoft.com/devcontainers/dotnet",
            }
            """);

        Assert.Equal("/src/" + FolderName, DevContainerConfig.Find(_folder.Path)?.ContainerFolder);
    }

    [Theory]
    [InlineData("""{ "workspaceFolder": "${localEnv:HOME}/src" }""")]
    [InlineData("""{ "workspaceFolder": "relative" }""")]
    [InlineData("""{ "workspaceFolder": 42 }""")]
    [InlineData("""{ "workspaceFolder": "" }""")]
    [InlineData("not json")]
    [InlineData("[]")]
    public void Find_WithUnusableWorkspaceFolder_UsesDefault(string contents)
    {
        _folder.AddFile(".devcontainer.json", contents);

        Assert.Equal("/workspaces/" + FolderName, DevContainerConfig.Find(_folder.Path)?.ContainerFolder);
    }

    [Fact]
    public void BuildFolderUri_EncodesHostPathAsHex()
    {
        var hex = Convert.ToHexStringLower(Encoding.UTF8.GetBytes("/home/me/work/api"));

        Assert.Equal(
            $"vscode-remote://dev-container+{hex}/workspaces/api",
            DevContainerConfig.BuildFolderUri("/home/me/work/api", "/workspaces/api"));
    }

    [Fact]
    public void BuildFolderUri_EncodesWindowsPathAndEscapesContainerFolder()
    {
        Assert.Equal(
            "vscode-remote://dev-container+433a5c4d7920576f726b/workspaces/my%20api",
            DevContainerConfig.BuildFolderUri(@"C:\My Work", "/workspaces/my api"));
    }
}
