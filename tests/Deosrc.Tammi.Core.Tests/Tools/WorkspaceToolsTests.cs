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
        var entry = Assert.Single(External(CreateTools(), Workspace, null, Custom(Editor)));

        Assert.Equal("Editor", entry.Label);
        Assert.Equal(_folder.Path, entry.Path);
        Assert.Null(entry.Repository);
        Assert.Same(Editor, entry.Tool);
        Assert.Equal("/usr/bin/editor", entry.Request!.FileName);
        Assert.Equal(["--new", _folder.Path], entry.Request!.Arguments);
        Assert.Equal(_folder.Path, entry.Request!.WorkingDirectory);
    }

    [Fact]
    public void GetEntries_FolderTool_OpensRepository()
    {
        var tool = Editor with { Arguments = ["{LaunchFolder}", "{RepositoryFolder}", "{WorkspaceFolder}"] };

        var entry = Assert.Single(External(CreateTools(), Workspace, _api, Custom(tool)));

        Assert.Equal(ApiFolder, entry.Path);
        Assert.Same(_api, entry.Repository);
        Assert.Equal([ApiFolder, ApiFolder, _folder.Path], entry.Request!.Arguments);
        Assert.Equal(ApiFolder, entry.Request!.WorkingDirectory);
    }

    [Fact]
    public void GetEntries_FillsInPlaceholdersInName()
    {
        var tool = Editor with { Name = "Edit {LaunchFolder}" };

        Assert.Equal($"Edit {ApiFolder}", Assert.Single(External(CreateTools(), Workspace, _api, Custom(tool))).Label);
    }

    [Fact]
    public void GetEntries_LeavesOutEntriesUsingPlaceholdersWithoutValue()
    {
        var tool = Editor with { Arguments = ["{RepositoryFolder}"] };

        Assert.Empty(External(CreateTools(), Workspace, null, Custom(tool)));
        Assert.Single(External(CreateTools(), Workspace, _api, Custom(tool)));
        Assert.Empty(External(CreateTools(), Workspace, _api, Custom(Editor with { Name = "{FileName}" })));
    }

    [Theory]
    [InlineData(ToolScope.Both, true, true)]
    [InlineData(ToolScope.Workspace, true, false)]
    [InlineData(ToolScope.Repository, false, true)]
    public void GetEntries_FollowsScope(ToolScope scope, bool workspace, bool repository)
    {
        var settings = Custom(Editor with { Scope = scope });

        Assert.Equal(workspace, External(CreateTools(), Workspace, null, settings).Count == 1);
        Assert.Equal(repository, External(CreateTools(), Workspace, _api, settings).Count == 1);
    }

    [Fact]
    public void GetEntries_LeavesOutDisabledTools()
    {
        Assert.Empty(External(CreateTools(), Workspace, null, Custom(Editor with { Enabled = false })));
    }

    [Fact]
    public void GetEntries_LeavesOutToolsWhoseExecutableIsNotFound()
    {
        var tools = CreateTools(found: name => name == "ide" ? "/usr/bin/ide" : null);

        Assert.Empty(External(tools, Workspace, null, Custom(Editor, Editor with { Executable = " " })));
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

        var entry = Assert.Single(External(tools, 
            Workspace, null, Custom(Editor with { Executable = "%TAMMI_TEST_TOOLS%/editor" })));

        Assert.Equal("/opt/tools/editor", searched);
        Assert.Equal("/opt/tools/editor", entry.Request!.FileName);
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
            External(CreateTools(), Workspace, null, settings).Select(entry => entry.Label));
    }

    [Fact]
    public void GetEntries_FileTool_HasEntryForEachFileInRepository()
    {
        var api = _folder.AddFile(Path.Combine("api", "Api.sln"));
        var tools = _folder.AddFile(Path.Combine("api", "tools", "Tools.slnx"));
        _folder.AddFile(Path.Combine("web", "Web.sln"));

        var entries = External(CreateTools(), Workspace, _api, Custom(SolutionTool));

        Assert.Equal(["IDE - Api", "IDE - Tools"], entries.Select(entry => entry.Label));
        Assert.Equal([api, tools], entries.Select(entry => entry.Path));
        Assert.Equal([[api], [tools]], entries.Select(entry => entry.Request!.Arguments));
        Assert.Equal(Path.GetDirectoryName(tools), entries[1].Request!.WorkingDirectory);
        Assert.All(entries, entry => Assert.Same(_api, entry.Repository));
    }

    [Fact]
    public void GetEntries_FileTool_ForWorkspaceFolder_HasFilesOfEveryRepository()
    {
        var api = _folder.AddFile(Path.Combine("api", "Api.sln"));
        var web = _folder.AddFile(Path.Combine("web", "src", "Web.slnx"));

        var entries = External(CreateTools(), Workspace, null, Custom(SolutionTool));

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

        var entry = Assert.Single(External(CreateTools(), Workspace, null, Custom(tool)));

        Assert.Equal("Api.sln", entry.Label);
        Assert.Equal(
            [Path.GetDirectoryName(file)!, "Api.sln", "Api", file, ApiFolder, _folder.Path, _folder.Path],
            entry.Request!.Arguments);
    }

    [Fact]
    public void GetEntries_FileTool_WithoutFiles_HasNoEntries()
    {
        _folder.AddFile(Path.Combine("api", "README.md"));

        Assert.Empty(External(CreateTools(), Workspace, _api, Custom(SolutionTool)));
        Assert.Empty(External(CreateTools(), Workspace, null, Custom(SolutionTool with { FilePatterns = [] })));
    }

    [Fact]
    public void GetEntries_DevContainer_OnlyForRepositoriesWithConfig()
    {
        _folder.AddFile(Path.Combine("api", ".devcontainer", "devcontainer.json"), "{}");
        var settings = new ToolSettings();

        var entry = Assert.Single(
            External(CreateTools(), Workspace, _api, settings),
            entry => entry.Tool.Id == PredefinedTools.DevContainer.Id);

        Assert.Equal(["open", ApiFolder], entry.Request!.Arguments);
        Assert.DoesNotContain(
            External(CreateTools(), Workspace, _web, settings), e => e.Tool.Id == PredefinedTools.DevContainer.Id);
        Assert.DoesNotContain(
            External(CreateTools(), Workspace, null, settings), e => e.Tool.Id == PredefinedTools.DevContainer.Id);
    }

    [Fact]
    public void GetEntries_TerminalIsFirst_AndAlwaysOffered()
    {
        var tools = CreateTools(found: _ => null);
        var settings = new ToolSettings
        {
            DisabledPredefinedTools = [PredefinedTools.Terminal.Id!],
            CustomTools = [Editor],
        };

        var workspace = tools.GetEntries(Workspace, null, settings);
        var repository = tools.GetEntries(Workspace, _api, settings);

        var entry = Assert.Single(workspace);
        Assert.True(entry.OpensTerminal);
        Assert.Equal("Terminal", entry.Label);
        Assert.Equal(_folder.Path, entry.Path);
        Assert.Null(entry.Repository);
        Assert.Null(entry.Request);
        Assert.Same(_api, Assert.Single(repository).Repository);
        Assert.Equal(ApiFolder, repository[0].Path);
    }

    [Fact]
    public void GetEntries_TerminalComesBeforeOtherTools()
    {
        var entries = CreateTools().GetEntries(Workspace, null, Custom(Editor));

        Assert.Equal(["Terminal", "Editor"], entries.Select(entry => entry.Label));
    }

    [Fact]
    public void GroupByTool_FolderTool_HasWorkspaceAndEachRepository()
    {
        var items = Group(Custom(Editor));

        var editor = Assert.Single(items, item => item.Tool == Editor);
        Assert.Equal("Editor", editor.Label);
        Assert.Null(editor.FilePath);
        Assert.Equal(_folder.Path, editor.WorkspaceEntry!.Path);
        Assert.Equal([_api, _web], editor.RepositoryEntries.Select(entry => entry.Repository));
        Assert.Equal([_folder.Path, ApiFolder, Path.Combine(_folder.Path, "web")], editor.Entries.Select(e => e.Path));
    }

    [Fact]
    public void GroupByTool_RepositoryOnlyTool_IsListedWithoutWorkspaceEntry()
    {
        var items = Group(Custom(Editor with { Scope = ToolScope.Repository }));

        var editor = Assert.Single(items, item => item.Tool.Name == "Editor");
        Assert.Null(editor.WorkspaceEntry);
        Assert.Equal("Editor", editor.Label);
        Assert.Equal(2, editor.Entries.Count);
    }

    [Fact]
    public void GroupByTool_WorkspaceOnlyTool_HasNoRepositoryEntries()
    {
        var editor = Assert.Single(
            Group(Custom(Editor with { Scope = ToolScope.Workspace })), item => item.Tool.Name == "Editor");

        Assert.NotNull(editor.WorkspaceEntry);
        Assert.Empty(editor.RepositoryEntries);
    }

    [Fact]
    public void GroupByTool_FileTool_HasItemForEachFile_WithItsRepositorysEntry()
    {
        var api = _folder.AddFile(Path.Combine("api", "Api.sln"));
        var web = _folder.AddFile(Path.Combine("web", "Web.sln"));

        var items = Group(Custom(SolutionTool)).Where(item => item.Tool.Kind == ToolKind.File).ToArray();

        Assert.Equal([api, web], items.Select(item => item.FilePath));
        Assert.Equal(["IDE - Api", "IDE - Web"], items.Select(item => item.Label));
        Assert.All(items, item => Assert.Equal(item.FilePath, item.WorkspaceEntry!.Path));
        Assert.Same(_api, Assert.Single(items[0].RepositoryEntries).Repository);
        Assert.Same(_web, Assert.Single(items[1].RepositoryEntries).Repository);
        Assert.All(items, item => Assert.All(item.Entries, entry => Assert.Equal(item.FilePath, entry.Path)));
    }

    [Fact]
    public void GroupByTool_FollowsToolOrder_TerminalFirst()
    {
        _folder.AddFile(Path.Combine("api", "Api.sln"));
        var settings = Custom(SolutionTool, Editor);

        var items = Group(settings);

        Assert.Equal(["Terminal", "IDE - Api", "Editor"], items.Select(item => item.Label));
        Assert.True(items[0].Tool.OpensTerminal);
        Assert.Null(items[0].WorkspaceEntry!.Repository);
        Assert.Equal([_api, _web], items[0].RepositoryEntries.Select(entry => entry.Repository));
    }

    [Fact]
    public void GroupByTool_WithoutEntries_IsEmpty()
    {
        Assert.Empty(WorkspaceTools.GroupByTool(new ToolSettings().Tools, [], [[], []]));
    }

    [Fact]
    public void Open_Terminal_Throws()
    {
        var entry = new ToolEntry(PredefinedTools.Terminal, "Terminal", "/work", null, null);

        Assert.Throws<InvalidOperationException>(() => CreateTools().Open(entry));
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

    // Groups the entries of the Workspace Folder and both repositories, as the Tools section lists them.
    private IReadOnlyList<ToolItem> Group(ToolSettings settings)
    {
        var tools = CreateTools();
        return WorkspaceTools.GroupByTool(
            settings.Tools,
            tools.GetEntries(Workspace, null, settings),
            [tools.GetEntries(Workspace, _api, settings), tools.GetEntries(Workspace, _web, settings)]);
    }

    // The entries of the tools that start an executable: every entry but the Terminal tool's, which is always there.
    private static IReadOnlyList<ToolEntry> External(
        WorkspaceTools tools,
        Workspace workspace,
        WorkspaceRepository? repository,
        ToolSettings settings) =>
        tools.GetEntries(workspace, repository, settings).Where(entry => !entry.OpensTerminal).ToArray();

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
