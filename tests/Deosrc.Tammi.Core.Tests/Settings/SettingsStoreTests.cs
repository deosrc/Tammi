using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Deosrc.Tammi.Core.Agents;
using Deosrc.Tammi.Core.Platform;
using Deosrc.Tammi.Core.Settings;

namespace Deosrc.Tammi.Core.Tests.Settings;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "tammi-tests", Guid.NewGuid().ToString("N"));

    // A folder that doesn't exist yet, as on first run.
    private string ConfigDirectory => Path.Combine(_root, "config");

    private string SettingsPath => Path.Combine(ConfigDirectory, SettingsStore.FileName);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Current_WithoutFile_ReturnsDefaultsWithoutWritingFile()
    {
        var store = CreateStore();

        Assert.Equal(new AppSettings(), store.Current);
        Assert.Null(store.LoadProblem);
        Assert.False(File.Exists(SettingsPath));
    }

    [Fact]
    public void Defaults_AreHomeWorkspacesFolderAndFiveMinutes()
    {
        var settings = new AppSettings();

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.Equal(Path.Combine(home, "Workspaces"), settings.General.DefaultWorkspaceParentFolder);
        Assert.Equal(TimeSpan.FromMinutes(5), settings.Git.StatusRefreshInterval);
    }

    [Fact]
    public void Constructor_UsesAppConfigDirectory()
    {
        var store = new SettingsStore(new TestAppPaths(ConfigDirectory));

        Assert.Equal(SettingsPath, store.FilePath);
    }

    [Fact]
    public async Task UpdateAsync_SavesSettingsThatANewStoreLoads()
    {
        var store = CreateStore();
        var parentFolder = Path.Combine(_root, "workspaces");

        await store.UpdateAsync(settings => settings with
        {
            General = settings.General with { DefaultWorkspaceParentFolder = parentFolder },
            Git = settings.Git with { StatusRefreshInterval = TimeSpan.FromMinutes(15) },
        }, TestContext.Current.CancellationToken);

        var reloaded = CreateStore().Current;
        Assert.Equal(parentFolder, reloaded.General.DefaultWorkspaceParentFolder);
        Assert.Equal(TimeSpan.FromMinutes(15), reloaded.Git.StatusRefreshInterval);
    }

    [Fact]
    public void Defaults_SuggestLowercaseHyphenatedNamesWithTheTaskReference()
    {
        Assert.Equal(
            new WorkspaceNameFormat
            {
                Separator = WorkspaceNameSeparator.Hyphens,
                Case = WorkspaceNameCase.Lowercase,
                IncludeTaskReference = true,
            },
            new AppSettings().General.WorkspaceNameFormat);
    }

    [Fact]
    public async Task UpdateAsync_SavesTheWorkspaceNameFormat_ByName()
    {
        var format = new WorkspaceNameFormat
        {
            Separator = WorkspaceNameSeparator.Spaces,
            Case = WorkspaceNameCase.AsWritten,
            IncludeTaskReference = false,
        };

        await CreateStore().UpdateAsync(
            settings => settings with { General = settings.General with { WorkspaceNameFormat = format } },
            TestContext.Current.CancellationToken);

        Assert.Equal(format, CreateStore().Current.General.WorkspaceNameFormat);
        string json = File.ReadAllText(SettingsPath);
        Assert.Contains("\"Spaces\"", json);
        Assert.Contains("\"AsWritten\"", json);
    }

    [Fact]
    public void Load_WithoutAWorkspaceNameFormat_UsesTheDefault()
    {
        WriteSettingsFile("""{"version":1,"general":{"defaultWorkspaceParentFolder":"/tmp"}}""");

        Assert.Equal(new WorkspaceNameFormat(), CreateStore().Current.General.WorkspaceNameFormat);
    }

    [Fact]
    public void Load_WithAnUnknownWorkspaceNameFormatNumber_UsesTheDefault()
    {
        WriteSettingsFile("""{"version":1,"general":{"workspaceNameFormat":{"separator":9,"case":9}}}""");

        WorkspaceNameFormat format = CreateStore().Current.General.WorkspaceNameFormat;

        Assert.Equal(WorkspaceNameSeparator.Hyphens, format.Separator);
        Assert.Equal(WorkspaceNameCase.Lowercase, format.Case);
    }

    [Fact]
    public void Defaults_LogFromInformation()
    {
        Assert.Equal(LogLevel.Information, new AppSettings().General.LogLevel);
    }

    [Fact]
    public async Task UpdateAsync_SavesTheLogLevel_ByName()
    {
        await CreateStore().UpdateAsync(
            settings => settings with { General = settings.General with { LogLevel = LogLevel.Debug } },
            TestContext.Current.CancellationToken);

        Assert.Equal(LogLevel.Debug, CreateStore().Current.General.LogLevel);
        Assert.Contains("\"logLevel\": \"Debug\"", File.ReadAllText(SettingsPath));
    }

    [Theory]
    [InlineData("9")]
    [InlineData("\"None\"")]
    public void Load_WithAnUnknownOrNoLogLevel_LogsFromInformation(string logLevel)
    {
        WriteSettingsFile($$$"""{"version":1,"general":{"logLevel":{{{logLevel}}}}}""");

        Assert.Equal(LogLevel.Information, CreateStore().Current.General.LogLevel);
    }

    [Fact]
    public async Task UpdateAsync_WritesVersionFirst()
    {
        var store = CreateStore();

        await store.UpdateAsync(settings => settings, TestContext.Current.CancellationToken);

        var json = JsonNode.Parse(File.ReadAllText(SettingsPath))!.AsObject();
        Assert.Equal("version", json.First().Key);
        Assert.Equal(1, json["version"]!.GetValue<int>());
        Assert.NotNull(json["general"]?["defaultWorkspaceParentFolder"]);
        Assert.NotNull(json["git"]?["statusRefreshInterval"]);
        Assert.False(File.Exists(SettingsPath + ".tmp"));
    }

    [Fact]
    public async Task UpdateAsync_UpdatesCurrentAndRaisesChanged()
    {
        var store = CreateStore();
        var changes = 0;
        store.Changed += () => changes++;

        var saved = await store.UpdateAsync(settings => settings with
        {
            Git = settings.Git with { StatusRefreshInterval = TimeSpan.FromMinutes(2) },
        }, TestContext.Current.CancellationToken);

        Assert.Equal(TimeSpan.FromMinutes(2), store.Current.Git.StatusRefreshInterval);
        Assert.Same(saved, store.Current);
        Assert.Equal(1, changes);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(2000, 1440)]
    public async Task UpdateAsync_ClampsStatusRefreshInterval(int minutes, int expectedMinutes)
    {
        var store = CreateStore();

        var saved = await store.UpdateAsync(settings => settings with
        {
            Git = settings.Git with { StatusRefreshInterval = TimeSpan.FromMinutes(minutes) },
        }, TestContext.Current.CancellationToken);

        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), saved.Git.StatusRefreshInterval);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateAsync_WithBlankParentFolder_UsesDefault(string folder)
    {
        var store = CreateStore();

        var saved = await store.UpdateAsync(settings => settings with
        {
            General = settings.General with { DefaultWorkspaceParentFolder = folder },
        }, TestContext.Current.CancellationToken);

        Assert.Equal(GeneralSettings.DefaultDefaultWorkspaceParentFolder, saved.General.DefaultWorkspaceParentFolder);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateAsync_WithBlankInitialPromptTemplate_UsesDefault(string template)
    {
        var store = CreateStore();

        var saved = await store.UpdateAsync(settings => settings with
        {
            Agents = settings.Agents with { InitialPromptTemplate = template },
        }, TestContext.Current.CancellationToken);

        Assert.Equal(InitialPrompt.DefaultTemplate, saved.Agents.InitialPromptTemplate);
    }

    [Fact]
    public async Task UpdateAsync_SavesInitialPromptTemplate()
    {
        var template = "Read {contextFile}.\nThen \"start\" & say 100%.";
        await CreateStore().UpdateAsync(settings => settings with
        {
            Agents = settings.Agents with { InitialPromptTemplate = template },
        }, TestContext.Current.CancellationToken);

        Assert.Equal(template, CreateStore().Current.Agents.InitialPromptTemplate);
    }

    [Fact]
    public void Defaults_HaveSupportedAgentsOnlyAndPlatformShell()
    {
        var settings = new AppSettings();

        Assert.Empty(settings.Agents.CustomDefinitions);
        Assert.Equal([AgentDefinition.ClaudeCode], settings.Agents.Definitions);
        Assert.Null(settings.Tools.TerminalShell);
        Assert.Empty(settings.Tools.TerminalShellArguments);
    }

    [Fact]
    public async Task UpdateAsync_SavesCustomAgentsThatANewStoreLoads()
    {
        AgentDefinition[] definitions =
        [
            new AgentDefinition("Codex", "codex", "--full-auto")
            {
                PromptArguments = ["{prompt}"],
                SessionIdArguments = ["--id", "{sessionId}"],
                ResumeArguments = ["resume", "{sessionId}"],
            },
            new AgentDefinition("No prompt", "/opt/agent", "a b", "\"quoted\""),
        ];

        await CreateStore().UpdateAsync(settings => settings with
        {
            Agents = settings.Agents with { CustomDefinitions = definitions },
        }, TestContext.Current.CancellationToken);

        var reloaded = CreateStore().Current.Agents;
        Assert.Equal(definitions, reloaded.CustomDefinitions);
        Assert.False(reloaded.CustomDefinitions[1].AcceptsInitialPrompt);
        Assert.Equal([AgentDefinition.ClaudeCode, .. definitions], reloaded.Definitions);
    }

    [Fact]
    public async Task UpdateAsync_WritesCustomAgentsOnly_WithoutComputedValues()
    {
        await CreateStore().UpdateAsync(settings => settings with
        {
            Agents = settings.Agents with { CustomDefinitions = [new AgentDefinition("Custom", "custom")] },
        }, TestContext.Current.CancellationToken);

        var agents = JsonNode.Parse(File.ReadAllText(SettingsPath))!["agents"]!.AsObject();
        Assert.Equal(["customDefinitions", "initialPromptTemplate"], agents.Select(p => p.Key));
        var definition = Assert.Single(agents["customDefinitions"]!.AsArray())!.AsObject();
        Assert.Equal(
            ["name", "command", "arguments", "promptArguments", "sessionIdArguments", "resumeArguments"],
            definition.Select(p => p.Key));
    }

    [Fact]
    public async Task UpdateAsync_TrimsAgentCommandAndKeepsBlankOne()
    {
        var saved = await CreateStore().UpdateAsync(settings => settings with
        {
            Agents = settings.Agents with
            {
                CustomDefinitions = [new AgentDefinition(" Agent ", "  agent  "), new AgentDefinition("", "")],
            },
        }, TestContext.Current.CancellationToken);

        Assert.Equal(
            [new AgentDefinition(" Agent ", "agent"), new AgentDefinition("", "")],
            saved.Agents.CustomDefinitions);
    }

    [Fact]
    public void Current_WithMissingAgentValues_FillsThemIn()
    {
        WriteSettingsFile("""
            {
              "version": 1,
              "agents": {
                "customDefinitions": [
                  null,
                  { "name": "Bare" },
                  { "command": "agent", "arguments": null, "promptArguments": ["-p", null, "{prompt}"] }
                ]
              }
            }
            """);

        var store = CreateStore();

        Assert.Null(store.LoadProblem);
        Assert.Equal(
            [
                new AgentDefinition("Bare", ""),
                new AgentDefinition("", "agent") { PromptArguments = ["-p", "{prompt}"] },
            ],
            store.Current.Agents.CustomDefinitions);
    }

    [Fact]
    public async Task UpdateAsync_SavesTerminalShellThatANewStoreLoads()
    {
        await CreateStore().UpdateAsync(settings => settings with
        {
            Tools = settings.Tools with { TerminalShell = " /usr/bin/fish ", TerminalShellArguments = ["-l", "a b"] },
        }, TestContext.Current.CancellationToken);

        var tools = CreateStore().Current.Tools;
        Assert.Equal("/usr/bin/fish", tools.TerminalShell);
        Assert.Equal(["-l", "a b"], tools.TerminalShellArguments);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateAsync_WithBlankTerminalShell_UsesDefaultShellWithoutArguments(string? shell)
    {
        var saved = await CreateStore().UpdateAsync(settings => settings with
        {
            Tools = settings.Tools with { TerminalShell = shell, TerminalShellArguments = ["-l"] },
        }, TestContext.Current.CancellationToken);

        Assert.Equal(new ToolSettings(), saved.Tools);
    }

    [Fact]
    public async Task UpdateAsync_ConcurrentUpdates_AreAllApplied()
    {
        var store = CreateStore();

        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => store.UpdateAsync(settings =>
            settings with
            {
                Git = settings.Git with
                {
                    StatusRefreshInterval = settings.Git.StatusRefreshInterval + TimeSpan.FromMinutes(1),
                },
            },
            TestContext.Current.CancellationToken))));

        Assert.Equal(TimeSpan.FromMinutes(25), store.Current.Git.StatusRefreshInterval);
        Assert.Equal(TimeSpan.FromMinutes(25), CreateStore().Current.Git.StatusRefreshInterval);
    }

    [Fact]
    public async Task UpdateAsync_WhenCancelled_DoesNotChangeSettings()
    {
        var store = CreateStore();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.UpdateAsync(
            settings => settings with
            {
                Git = settings.Git with { StatusRefreshInterval = TimeSpan.FromMinutes(9) },
            },
            cancellation.Token));

        Assert.Equal(GitSettings.DefaultStatusRefreshInterval, store.Current.Git.StatusRefreshInterval);
        Assert.False(File.Exists(SettingsPath));
    }

    [Fact]
    public void Current_WithMissingProperties_UsesDefaultsForThem()
    {
        WriteSettingsFile("""{ "version": 1, "git": { "statusRefreshInterval": "00:10:00" } }""");

        var settings = CreateStore().Current;

        Assert.Equal(TimeSpan.FromMinutes(10), settings.Git.StatusRefreshInterval);
        Assert.Equal(
            GeneralSettings.DefaultDefaultWorkspaceParentFolder,
            settings.General.DefaultWorkspaceParentFolder);
    }

    [Theory]
    [InlineData("""{ "version": 1 }""")]
    [InlineData("""{ "version": 1, "general": null }""")]
    [InlineData("""{ "version": 1, "general": { "defaultWorkspaceParentFolder": null } }""")]
    [InlineData("""{ "version": 1, "git": null }""")]
    [InlineData("""{ "version": 1, "git": { "monitoredFolders": null, "localRepositories": null } }""")]
    [InlineData("""{ "version": 1, "agents": null }""")]
    [InlineData("""{ "version": 1, "agents": { "initialPromptTemplate": null } }""")]
    [InlineData("""{ "version": 1, "agents": { "customDefinitions": null } }""")]
    [InlineData("""{ "version": 1, "tools": null }""")]
    [InlineData("""{ "version": 1, "tools": { "terminalShell": null, "terminalShellArguments": null } }""")]
    [InlineData("""{ }""")]
    public void Current_WithMissingOrNullValues_UsesDefaults(string json)
    {
        WriteSettingsFile(json);

        var store = CreateStore();

        Assert.Equal(new AppSettings(), store.Current);
        Assert.Null(store.LoadProblem);
    }

    [Fact]
    public async Task UpdateAsync_SavesFoldersAsFullPathsOnce()
    {
        var store = CreateStore();
        var repos = Path.Combine(_root, "repos");
        var api = Path.Combine(_root, "api");

        await store.UpdateAsync(settings => settings with
        {
            Git = settings.Git with
            {
                MonitoredFolders = [repos + Path.DirectorySeparatorChar, "  ", $" {repos} ", Path.Combine(_root, "more")],
                LocalRepositories = [api, api + Path.DirectorySeparatorChar, ""],
            },
        }, TestContext.Current.CancellationToken);

        var reloaded = CreateStore().Current.Git;
        Assert.Equal([repos, Path.Combine(_root, "more")], reloaded.MonitoredFolders);
        Assert.Equal([api], reloaded.LocalRepositories);
        Assert.Equal(store.Current.Git, reloaded);
    }

    [Fact]
    public void GitSettings_WithEqualFolderLists_AreEqual()
    {
        var first = new GitSettings { MonitoredFolders = ["/a", "/b"], LocalRepositories = ["/c"] };
        var second = new GitSettings { MonitoredFolders = ["/a", "/b"], LocalRepositories = ["/c"] };

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(first, second with { MonitoredFolders = ["/b", "/a"] });
        Assert.NotEqual(first, second with { LocalRepositories = [] });
    }

    [Fact]
    public void Current_WithOutOfRangeInterval_ClampsIt()
    {
        WriteSettingsFile("""{ "version": 1, "git": { "statusRefreshInterval": "00:00:05" } }""");

        Assert.Equal(GitSettings.MinStatusRefreshInterval, CreateStore().Current.Git.StatusRefreshInterval);
    }

    [Fact]
    public void Current_IgnoresUnknownPropertiesAndComments()
    {
        WriteSettingsFile("""
            {
              // Written by hand
              "version": 1,
              "unknown": { "a": 1 },
              "git": { "statusRefreshInterval": "00:20:00", "unknown": true, },
            }
            """);

        var store = CreateStore();

        Assert.Equal(TimeSpan.FromMinutes(20), store.Current.Git.StatusRefreshInterval);
        Assert.Null(store.LoadProblem);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{ "version": "one" }""")]
    [InlineData("""{ "version": 0 }""")]
    [InlineData("""{ "version": 1, "git": { "statusRefreshInterval": "soon" } }""")]
    public void Current_WithInvalidFile_UsesDefaultsAndKeepsCopy(string contents)
    {
        WriteSettingsFile(contents);

        var store = CreateStore();

        Assert.Equal(new AppSettings(), store.Current);
        Assert.NotNull(store.LoadProblem);
        Assert.Contains(SettingsPath, store.LoadProblem);
        Assert.Equal(contents, File.ReadAllText(Path.Combine(ConfigDirectory, SettingsStore.InvalidFileName)));
    }

    [Fact]
    public async Task UpdateAsync_AfterInvalidFile_ReplacesItAndClearsLoadProblem()
    {
        WriteSettingsFile("not json");
        var store = CreateStore();
        Assert.NotNull(store.LoadProblem);

        await store.UpdateAsync(settings => settings, TestContext.Current.CancellationToken);

        Assert.Null(store.LoadProblem);
        Assert.Null(CreateStore().LoadProblem);
        Assert.Equal("not json", File.ReadAllText(Path.Combine(ConfigDirectory, SettingsStore.InvalidFileName)));
    }

    [Fact]
    public void Current_WithOlderVersion_RunsMigrationsInOrder()
    {
        WriteSettingsFile("""{ "version": 1, "refreshMinutes": 7 }""");
        var applied = new List<int>();
        IReadOnlyList<Action<JsonObject>> migrations =
        [
            // Version 1 to 2: moves a top-level property into the Git section.
            json =>
            {
                applied.Add(1);
                var minutes = json["refreshMinutes"]!.GetValue<int>();
                json.Remove("refreshMinutes");
                json["git"] = new JsonObject
                {
                    ["statusRefreshInterval"] = TimeSpan.FromMinutes(minutes).ToString("c"),
                };
            },
            // Version 2 to 3: changes nothing.
            _ => applied.Add(2),
        ];

        var store = new SettingsStore(ConfigDirectory, migrations);

        Assert.Equal(TimeSpan.FromMinutes(7), store.Current.Git.StatusRefreshInterval);
        Assert.Equal([1, 2], applied);
        Assert.Equal(3, store.CurrentVersion);
    }

    [Fact]
    public void Current_WithCurrentVersion_RunsNoMigrations()
    {
        WriteSettingsFile("""{ "version": 2 }""");
        var applied = 0;

        _ = new SettingsStore(ConfigDirectory, [_ => applied++]).Current;

        Assert.Equal(0, applied);
    }

    [Fact]
    public void Current_WithoutVersion_TreatsFileAsVersionOne()
    {
        WriteSettingsFile("""{ }""");
        var applied = 0;

        _ = new SettingsStore(ConfigDirectory, [_ => applied++]).Current;

        Assert.Equal(1, applied);
    }

    [Fact]
    public void Current_WithNewerVersion_ReadsKnownSettings()
    {
        WriteSettingsFile("""{ "version": 99, "git": { "statusRefreshInterval": "00:30:00" }, "future": 1 }""");

        var store = CreateStore();

        Assert.Equal(TimeSpan.FromMinutes(30), store.Current.Git.StatusRefreshInterval);
        Assert.Null(store.LoadProblem);
    }

    private SettingsStore CreateStore() => new(ConfigDirectory, []);

    private void WriteSettingsFile(string contents)
    {
        Directory.CreateDirectory(ConfigDirectory);
        File.WriteAllText(SettingsPath, contents);
    }

    private sealed class TestAppPaths(string configDirectory) : IAppPaths
    {
        public string ConfigDirectory => configDirectory;

        public string DataDirectory => throw new NotSupportedException();

        public string CacheDirectory => throw new NotSupportedException();

        public string LogDirectory => throw new NotSupportedException();
    }
}
