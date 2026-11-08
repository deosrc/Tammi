using Deosrc.Tammi.Core.Platform;
using Deosrc.Tammi.Core.Settings;
using Deosrc.Tammi.Core.Tools;
using Deosrc.Tammi.Core.Workspaces;

namespace Deosrc.Tammi.Core.Tests.Tools;

public sealed class WorkspaceToolsTests : IDisposable
{
    private readonly TempFolder _folder = new();
    private readonly WorkspaceRepository _api = new("api", "/repos/api", "acme", "origin/main");
    private readonly WorkspaceRepository _web = new("web", "/repos/web", "acme", "origin/main");

    public void Dispose() => _folder.Dispose();

    private Workspace Workspace => new()
    {
        Id = "ws",
        Name = "acme",
        Folder = _folder.Path,
        Repositories = [_api, _web],
    };

    private string ApiFolder => Path.Combine(_folder.Path, "api");

    private static ToolDefinition Editor { get; } = new()
    {
        Name = "Editor",
        Executable = "editor",
        Arguments = ["--new", "{LaunchFolder}"],
    };

    private static ToolDefinition SolutionTool { get; } = new()
    {
        Kind = ToolKind.File,
        Name = "IDE - {FileNameWithoutExtension}",
        FilePatterns = ["*.sln", "*.slnx"],
        Executable = "ide",
        Arguments = ["{FilePath}"],
    };

    // No predefined tools, so only the custom ones are offered.
    private static ToolSettings Custom(params ToolDefinition[] tools) => new()
    {
        DisabledPredefinedTools = PredefinedTools.All.Select(tool => tool.Id!).ToArray(),
        CustomTools = tools,
    };

    [Fact]
    public void GetEntries_FolderTool_OpensWorkspaceFolder()
    {
        var entry = Assert.Single(CreateTools().GetEntries(Workspace, null, Custom(Editor)));

        Assert.Equal("Editor", entry.Label);
        Assert.Equal(_folder.Path, entry.Path);
        Assert.Null(entry.Repository);
        Assert.Same(Editor, entry.Tool);
        Assert.Equal("/usr/bin/editor", entry.Request.FileName);
        Assert.Equal(["--new", _folder.Path], entry.Request.Arguments);
        Assert.Equal(_folder.Path, entry.Request.WorkingDirectory);
    }

    [Fact]
    public void GetEntries_FolderTool_OpensRepository()
    {
        var tool = Editor with { Arguments = ["{LaunchFolder}", "{RepositoryFolder}", "{WorkspaceFolder}"] };

        var entry = Assert.Single(CreateTools().GetEntries(Workspace, _api, Custom(tool)));

        Assert.Equal(ApiFolder, entry.Path);
        Assert.Same(_api, entry.Repository);
        Assert.Equal([ApiFolder, ApiFolder, _folder.Path], entry.Request.Arguments);
        Assert.Equal(ApiFolder, entry.Request.WorkingDirectory);
    }

    [Fact]
    public void GetEntries_FillsInPlaceholdersInName()
    {
        var tool = Editor with { Name = "Edit {LaunchFolder}" };

        Assert.Equal($"Edit {ApiFolder}", Assert.Single(CreateTools().GetEntries(Workspace, _api, Custom(tool))).Label);
    }

    [Fact]
    public void GetEntries_LeavesOutEntriesUsingPlaceholdersWithoutValue()
    {
        var tool = Editor with { Arguments = ["{RepositoryFolder}"] };

        Assert.Empty(CreateTools().GetEntries(Workspace, null, Custom(tool)));
        Assert.Single(CreateTools().GetEntries(Workspace, _api, Custom(tool)));
        Assert.Empty(CreateTools().GetEntries(Workspace, _api, Custom(Editor with { Name = "{FileName}" })));
    }

    [Theory]
    [InlineData(ToolScope.Both, true, true)]
    [InlineData(ToolScope.Workspace, true, false)]
    [InlineData(ToolScope.Repository, false, true)]
    public void GetEntries_FollowsScope(ToolScope scope, bool workspace, bool repository)
    {
        var settings = Custom(Editor with { Scope = scope });

        Assert.Equal(workspace, CreateTools().GetEntries(Workspace, null, settings).Count == 1);
        Assert.Equal(repository, CreateTools().GetEntries(Workspace, _api, settings).Count == 1);
    }

    [Fact]
    public void GetEntries_LeavesOutDisabledTools()
    {
        Assert.Empty(CreateTools().GetEntries(Workspace, null, Custom(Editor with { Enabled = false })));
    }

    [Fact]
    public void GetEntries_LeavesOutToolsWhoseExecutableIsNotFound()
    {
        var tools = CreateTools(found: name => name == "ide" ? "/usr/bin/ide" : null);

        Assert.Empty(tools.GetEntries(Workspace, null, Custom(Editor, Editor with { Executable = " " })));
    }

    [Fact]
    public void GetEntries_ExpandsEnvironmentVariablesInExecutable()
    {
        Environment.SetEnvironmentVariable("TAMMI_TEST_TOOLS", "/opt/tools");
        string? searched = null;
        var tools = CreateTools(found: name =>
        {
            searched = name;
            return name;
        });

        var entry = Assert.Single(tools.GetEntries(
            Workspace, null, Custom(Editor with { Executable = "%TAMMI_TEST_TOOLS%/editor" })));

        Assert.Equal("/opt/tools/editor", searched);
        Assert.Equal("/opt/tools/editor", entry.Request.FileName);
    }

    [Fact]
    public void GetEntries_OffersToolsInOrder_PredefinedFirst()
    {
        var settings = new ToolSettings { CustomTools = [Editor, Editor with { Name = "Second" }] };

        var labels = CreateTools().GetEntries(Workspace, null, settings).Select(entry => entry.Label).ToArray();

        Assert.Equal(
            [.. PredefinedTools.All.Where(tool => tool.Kind == ToolKind.Folder).Select(tool => tool.Name), "Editor", "Second"],
            labels);
    }

    [Fact]
    public void GetEntries_LeavesOutDisabledPredefinedTools()
    {
        var settings = new ToolSettings().WithEnabled(PredefinedTools.VsCode, false);

        Assert.DoesNotContain(
            PredefinedTools.VsCode.Name,
            CreateTools().GetEntries(Workspace, null, settings).Select(entry => entry.Label));
    }

    [Fact]
    public void GetEntries_FileTool_HasEntryForEachFileInRepository()
    {
        var api = _folder.AddFile(Path.Combine("api", "Api.sln"));
        var tools = _folder.AddFile(Path.Combine("api", "tools", "Tools.slnx"));
        _folder.AddFile(Path.Combine("web", "Web.sln"));

        var entries = CreateTools().GetEntries(Workspace, _api, Custom(SolutionTool));

        Assert.Equal(["IDE - Api", "IDE - Tools"], entries.Select(entry => entry.Label));
        Assert.Equal([api, tools], entries.Select(entry => entry.Path));
        Assert.Equal([[api], [tools]], entries.Select(entry => entry.Request.Arguments));
        Assert.Equal(Path.GetDirectoryName(tools), entries[1].Request.WorkingDirectory);
        Assert.All(entries, entry => Assert.Same(_api, entry.Repository));
    }

    [Fact]
    public void GetEntries_FileTool_ForWorkspaceFolder_HasFilesOfEveryRepository()
    {
        var api = _folder.AddFile(Path.Combine("api", "Api.sln"));
        var web = _folder.AddFile(Path.Combine("web", "src", "Web.slnx"));

        var entries = CreateTools().GetEntries(Workspace, null, Custom(SolutionTool));

        Assert.Equal([api, web], entries.Select(entry => entry.Path));
        Assert.Equal([_api, _web], entries.Select(entry => entry.Repository));
    }

    [Fact]
    public void GetEntries_FileTool_FillsInFilePlaceholders()
    {
        var file = _folder.AddFile(Path.Combine("api", "src", "Api.sln"));
        var tool = SolutionTool with
        {
            Name = "{FileName}",
            Arguments =
            [
                "{FileDirectory}", "{FileName}", "{FileNameWithoutExtension}", "{FilePath}",
                "{RepositoryFolder}", "{WorkspaceFolder}", "{LaunchFolder}",
            ],
        };

        var entry = Assert.Single(CreateTools().GetEntries(Workspace, null, Custom(tool)));

        Assert.Equal("Api.sln", entry.Label);
        Assert.Equal(
            [Path.GetDirectoryName(file)!, "Api.sln", "Api", file, ApiFolder, _folder.Path, _folder.Path],
            entry.Request.Arguments);
    }

    [Fact]
    public void GetEntries_FileTool_WithoutFiles_HasNoEntries()
    {
        _folder.AddFile(Path.Combine("api", "README.md"));

        Assert.Empty(CreateTools().GetEntries(Workspace, _api, Custom(SolutionTool)));
        Assert.Empty(CreateTools().GetEntries(Workspace, null, Custom(SolutionTool with { FilePatterns = [] })));
    }

    [Fact]
    public void GetEntries_DevContainer_OnlyForRepositoriesWithConfig()
    {
        _folder.AddFile(Path.Combine("api", ".devcontainer", "devcontainer.json"), "{}");
        var settings = new ToolSettings();

        var entry = Assert.Single(
            CreateTools().GetEntries(Workspace, _api, settings),
            entry => entry.Tool.Id == PredefinedTools.DevContainer.Id);

        Assert.Equal(["open", ApiFolder], entry.Request.Arguments);
        Assert.DoesNotContain(
            CreateTools().GetEntries(Workspace, _web, settings), e => e.Tool.Id == PredefinedTools.DevContainer.Id);
        Assert.DoesNotContain(
            CreateTools().GetEntries(Workspace, null, settings), e => e.Tool.Id == PredefinedTools.DevContainer.Id);
    }

    [Fact]
    public void Open_StartsProcessDetached_WithExecutableFound()
    {
        var launcher = new RecordingProcessLauncher(_ => "/opt/zed/zed");
        var entry = new ToolEntry(Editor, "Zed", "/work", null,
            new ProcessRequest("zed", "/work") { WorkingDirectory = "/work" });

        new WorkspaceTools(launcher).Open(entry);

        var request = Assert.Single(launcher.Detached);
        Assert.Equal("/opt/zed/zed", request.FileName);
        Assert.Equal(["/work"], request.Arguments);
        Assert.Equal("/work", request.WorkingDirectory);
        Assert.False(request.HideWindow);
    }

    [Fact]
    public void Open_WithBatchFileOnWindows_HidesItsWindow()
    {
        var launcher = new RecordingProcessLauncher(_ => @"C:\Toolbox\scripts\rider.cmd");
        var entry = new ToolEntry(Editor, "Rider", @"C:\work", null, new ProcessRequest("rider", @"C:\work"));

        new WorkspaceTools(launcher).Open(entry);

        Assert.Equal(OperatingSystem.IsWindows(), Assert.Single(launcher.Detached).HideWindow);
    }

    [Fact]
    public void Open_WithMissingExecutable_Throws()
    {
        var launcher = new RecordingProcessLauncher(_ => null);
        var entry = new ToolEntry(Editor, "Zed", "/work", null, new ProcessRequest("zed", "/work"));

        Assert.Throws<FileNotFoundException>(() => new WorkspaceTools(launcher).Open(entry));
        Assert.Empty(launcher.Detached);
    }

    private static WorkspaceTools CreateTools(Func<string, string?>? found = null) =>
        new(new RecordingProcessLauncher(found ?? (name => "/usr/bin/" + name)));

    private sealed class RecordingProcessLauncher(Func<string, string?> found) : IProcessLauncher
    {
        public List<ProcessRequest> Detached { get; } = [];

        public string? FindExecutable(string name) => found(name);

        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public int Start(ProcessRequest request) => throw new NotSupportedException();

        public void StartDetached(ProcessRequest request) => Detached.Add(request);

        public void Open(string target) => throw new NotSupportedException();
    }
}
