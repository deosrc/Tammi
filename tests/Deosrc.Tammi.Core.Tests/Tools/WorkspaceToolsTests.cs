using Deosrc.Tammi.Core.Platform;
using Deosrc.Tammi.Core.Settings;
using Deosrc.Tammi.Core.Tools;
using Deosrc.Tammi.Core.Workspaces;

namespace Deosrc.Tammi.Core.Tests.Tools;

public sealed class WorkspaceToolsTests : IDisposable
{
    private const string CodePath = "/usr/bin/code";
    private const string DevenvPath = @"C:\VS\devenv.exe";

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

    private static IReadOnlyList<InstalledTool> AllInstalled { get; } =
    [
        new(BuiltInTool.VsCode, CodePath),
        new(BuiltInTool.DevContainer, CodePath),
        new(BuiltInTool.VisualStudio, DevenvPath),
        new(BuiltInTool.FileManager, null),
    ];

    [Fact]
    public void GetEntries_ForWorkspaceFolder_OpensItInEachTool()
    {
        var entries = WorkspaceTools.GetEntries(Workspace, null, new ToolSettings(), AllInstalled);

        Assert.Equal(["VS Code", BuiltInTool.FileManager.Name], entries.Select(entry => entry.Label));
        Assert.All(entries, entry => Assert.Equal(_folder.Path, entry.Path));
        Assert.All(entries, entry => Assert.Null(entry.Repository));
        Assert.Equal(
            [ToolEntryKind.Process, ToolEntryKind.SystemDefault],
            entries.Select(entry => entry.Kind));

        var request = entries[0].Request!;
        Assert.Equal(CodePath, request.FileName);
        Assert.Equal([_folder.Path], request.Arguments);
        Assert.Equal(_folder.Path, request.WorkingDirectory);
    }

    [Fact]
    public void GetEntries_ForRepository_OpensItsWorktree()
    {
        var worktree = Path.Combine(_folder.Path, "api");

        var entries = WorkspaceTools.GetEntries(Workspace, _api, new ToolSettings(), AllInstalled);

        Assert.All(entries, entry => Assert.Equal(worktree, entry.Path));
        Assert.All(entries, entry => Assert.Same(_api, entry.Repository));
        Assert.Equal([worktree], entries.Single(entry => entry.Label == "VS Code").Request!.Arguments);
    }

    [Fact]
    public void GetEntries_LeavesOutToolsThatAreNotInstalled()
    {
        var entries = WorkspaceTools.GetEntries(
            Workspace, null, new ToolSettings(), [new(BuiltInTool.FileManager, null)]);

        Assert.Equal([BuiltInTool.FileManager.Name], entries.Select(entry => entry.Label));
    }

    [Fact]
    public void GetEntries_LeavesOutDisabledBuiltInTools()
    {
        var settings = new ToolSettings().WithBuiltIn(BuiltInTool.VsCode, s => s with { Enabled = false });

        var entries = WorkspaceTools.GetEntries(Workspace, null, settings, AllInstalled);

        Assert.DoesNotContain("VS Code", entries.Select(entry => entry.Label));
    }

    [Fact]
    public void GetEntries_FollowsBuiltInToolsScope()
    {
        var settings = new ToolSettings()
            .WithBuiltIn(BuiltInTool.VsCode, s => s with { Scope = ToolScope.Repository })
            .WithBuiltIn(BuiltInTool.FileManager, s => s with { Scope = ToolScope.Workspace });

        Assert.Equal(
            [BuiltInTool.FileManager.Name],
            WorkspaceTools.GetEntries(Workspace, null, settings, AllInstalled).Select(entry => entry.Label));
        Assert.Equal(
            ["VS Code"],
            WorkspaceTools.GetEntries(Workspace, _api, settings, AllInstalled).Select(entry => entry.Label));
    }

    [Fact]
    public void GetEntries_HasEntryForEachSolutionInRepository()
    {
        var api = _folder.AddFile(Path.Combine("api", "Api.sln"));
        var tools = _folder.AddFile(Path.Combine("api", "tools", "Tools.slnx"));
        _folder.AddFile(Path.Combine("web", "Web.sln"));

        var entries = SolutionEntries(_api);

        Assert.Equal(["Visual Studio: Api.sln", "Visual Studio: Tools.slnx"], entries.Select(entry => entry.Label));
        Assert.Equal([api, tools], entries.Select(entry => entry.Path));
        Assert.Equal([[api], [tools]], entries.Select(entry => entry.Request!.Arguments));
        Assert.Equal(DevenvPath, entries[0].Request!.FileName);
        Assert.Equal(Path.GetDirectoryName(tools), entries[1].Request!.WorkingDirectory);
    }

    [Fact]
    public void GetEntries_ForWorkspaceFolder_HasSolutionsOfEveryRepository()
    {
        var api = _folder.AddFile(Path.Combine("api", "Api.sln"));
        var web = _folder.AddFile(Path.Combine("web", "src", "Web.slnx"));

        var entries = SolutionEntries(null);

        Assert.Equal(["Visual Studio: Api.sln", "Visual Studio: Web.slnx"], entries.Select(entry => entry.Label));
        Assert.Equal([api, web], entries.Select(entry => entry.Path));
        Assert.Equal([_api, _web], entries.Select(entry => entry.Repository));
    }

    [Fact]
    public void GetEntries_TellsApartSolutionsWithTheSameName()
    {
        _folder.AddFile(Path.Combine("api", "App.sln"));
        _folder.AddFile(Path.Combine("web", "App.sln"));
        _folder.AddFile(Path.Combine("web", "Web.sln"));

        Assert.Equal(
            ["Visual Studio: api/App.sln", "Visual Studio: web/App.sln", "Visual Studio: Web.sln"],
            SolutionEntries(null).Select(entry => entry.Label));
    }

    [Fact]
    public void GetEntries_WithoutSolutions_HasNoSolutionEntries()
    {
        Assert.Empty(SolutionEntries(_api));
    }

    [Fact]
    public void GetEntries_OnlyOffersDevContainerForFoldersWithConfig()
    {
        _folder.AddFile(Path.Combine("api", ".devcontainer", "devcontainer.json"), "{}");
        var worktree = Path.Combine(_folder.Path, "api");

        var api = WorkspaceTools.GetEntries(Workspace, _api, new ToolSettings(), AllInstalled)
            .Single(entry => entry.Label == "Dev Container");

        Assert.Equal(CodePath, api.Request!.FileName);
        Assert.Equal(
            ["--folder-uri", DevContainerConfig.BuildFolderUri(worktree, "/workspaces/api")],
            api.Request.Arguments);
        Assert.DoesNotContain(
            "Dev Container", WorkspaceTools.GetEntries(Workspace, _web, new ToolSettings(), AllInstalled).Select(e => e.Label));
    }

    [Fact]
    public void GetEntries_AddsEnabledCustomToolsAfterBuiltInOnes()
    {
        var settings = new ToolSettings
        {
            CustomTools =
            [
                new ToolDefinition { Name = " Zed ", Executable = "zed", Arguments = ["--new", "{path}"] },
                new ToolDefinition { Name = "Off", Executable = "off", Enabled = false },
                new ToolDefinition { Name = "Repos only", Executable = "repo", Scope = ToolScope.Repository },
                new ToolDefinition { Name = "", Executable = "nameless", Icon = ToolIcon.Editor },
            ],
        };

        var entries = WorkspaceTools.GetEntries(Workspace, null, settings, [new(BuiltInTool.FileManager, null)]);

        Assert.Equal([BuiltInTool.FileManager.Name, "Zed", "Unnamed tool"], entries.Select(entry => entry.Label));
        Assert.Equal("zed", entries[1].Request!.FileName);
        Assert.Equal(["--new", _folder.Path], entries[1].Request!.Arguments);
        Assert.Equal(ToolIcon.Editor, entries[2].Icon);
        Assert.Contains(
            "Repos only", WorkspaceTools.GetEntries(Workspace, _api, settings, []).Select(entry => entry.Label));
    }

    [Fact]
    public void Open_StartsProcessDetached_WithExecutableFound()
    {
        var launcher = new RecordingProcessLauncher { Found = "/opt/zed/zed" };
        var entry = new ToolEntry("Zed", ToolIcon.Editor, ToolEntryKind.Process, "/work", null,
            new ProcessRequest("zed", "/work") { WorkingDirectory = "/work" });

        CreateTools(launcher).Open(entry);

        var request = Assert.Single(launcher.Detached);
        Assert.Equal("/opt/zed/zed", request.FileName);
        Assert.Equal(["/work"], request.Arguments);
        Assert.Equal("/work", request.WorkingDirectory);
        Assert.False(request.HideWindow);
    }

    [Fact]
    public void Open_WithBatchFileOnWindows_HidesItsWindow()
    {
        var launcher = new RecordingProcessLauncher { Found = @"C:\Toolbox\scripts\rider.cmd" };
        var entry = new ToolEntry("Rider", ToolIcon.Ide, ToolEntryKind.Process, @"C:\work", null,
            new ProcessRequest("rider", @"C:\work"));

        CreateTools(launcher).Open(entry);

        Assert.Equal(OperatingSystem.IsWindows(), Assert.Single(launcher.Detached).HideWindow);
    }

    [Fact]
    public void Open_WithMissingExecutable_Throws()
    {
        var launcher = new RecordingProcessLauncher { Found = null };
        var entry = new ToolEntry("Zed", ToolIcon.Editor, ToolEntryKind.Process, "/work", null,
            new ProcessRequest("zed", "/work"));

        Assert.Throws<FileNotFoundException>(() => CreateTools(launcher).Open(entry));
        Assert.Empty(launcher.Detached);
    }

    [Fact]
    public void Open_WithBlankExecutable_Throws()
    {
        var entry = new ToolEntry("Zed", ToolIcon.Editor, ToolEntryKind.Process, "/work", null,
            new ProcessRequest(" ", "/work"));

        Assert.Throws<FileNotFoundException>(() => CreateTools(new RecordingProcessLauncher()).Open(entry));
    }

    [Fact]
    public void Open_FileManager_OpensFolder()
    {
        var launcher = new RecordingProcessLauncher();
        var entry = new ToolEntry("Files", ToolIcon.Folder, ToolEntryKind.SystemDefault, "/work", null, null);

        CreateTools(launcher).Open(entry);

        Assert.Equal(["/work"], launcher.Opened);
    }

    [Fact]
    public void Open_ProcessWithoutRequest_Throws()
    {
        var entry = new ToolEntry("Zed", ToolIcon.Editor, ToolEntryKind.Process, "/work", null, null);

        Assert.Throws<InvalidOperationException>(() => CreateTools(new RecordingProcessLauncher()).Open(entry));
    }

    private IReadOnlyList<ToolEntry> SolutionEntries(WorkspaceRepository? repository) =>
        WorkspaceTools.GetEntries(Workspace, repository, new ToolSettings(), AllInstalled)
            .Where(entry => entry.Label.StartsWith("Visual Studio", StringComparison.Ordinal))
            .ToArray();

    private static WorkspaceTools CreateTools(IProcessLauncher launcher) => new(launcher, new BuiltInToolFinder(launcher));

    private sealed class RecordingProcessLauncher : IProcessLauncher
    {
        public string? Found { get; init; } = "/usr/bin/tool";

        public List<ProcessRequest> Detached { get; } = [];

        public List<string> Opened { get; } = [];

        public string? FindExecutable(string name) => Found;

        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public int Start(ProcessRequest request) => throw new NotSupportedException();

        public void StartDetached(ProcessRequest request) => Detached.Add(request);

        public void Open(string target) => Opened.Add(target);
    }
}
