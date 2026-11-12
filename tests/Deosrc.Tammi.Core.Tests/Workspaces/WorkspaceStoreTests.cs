using System.Text.Json.Nodes;
using Deosrc.Tammi.Core.Integrations;
using Deosrc.Tammi.Core.Platform;
using Deosrc.Tammi.Core.Workspaces;

namespace Deosrc.Tammi.Core.Tests.Workspaces;

public sealed class WorkspaceStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "tammi-tests", Guid.NewGuid().ToString("N"));

    // A folder that doesn't exist yet, as on first run.
    private string DataDirectory => Path.Combine(_root, "data");

    private string IndexPath => Path.Combine(DataDirectory, WorkspaceStore.IndexFileName);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Constructor_UsesAppDataDirectory()
    {
        var store = new WorkspaceStore(new TestAppPaths(DataDirectory));

        Assert.Equal(IndexPath, store.IndexFilePath);
    }

    [Fact]
    public async Task ListAsync_WithoutIndex_ReturnsNothingWithoutWritingFile()
    {
        var store = CreateStore();

        Assert.Empty(await store.ListAsync(Token));
        Assert.Null(store.IndexProblem);
        Assert.False(File.Exists(IndexPath));
    }

    [Fact]
    public async Task SaveAsync_WritesMetadataThatLoadAsyncReads()
    {
        var workspace = CreateWorkspace("Fix login");

        await CreateStore().SaveAsync(workspace, Token);
        var loaded = await CreateStore().LoadAsync(workspace.Folder, Token);

        AssertSameWorkspace(workspace, loaded);
    }

    [Fact]
    public async Task RenameAsync_SavesTheNewName_KeepingTheFolder()
    {
        var workspace = CreateWorkspace("fix-login");
        await CreateStore().SaveAsync(workspace, Token);
        var store = CreateStore();
        var changes = 0;
        store.Changed += () => changes++;

        var renamed = await store.RenameAsync(workspace.Id, "  api#42: Fix login  ", Token);

        Assert.Equal("api#42: Fix login", renamed.Name);
        Assert.Equal(workspace.Folder, renamed.Folder);
        Assert.Equal("api#42: Fix login", (await CreateStore().LoadAsync(workspace.Folder, Token)).Name);
        var listing = Assert.Single(await CreateStore().ListAsync(Token));
        Assert.Equal("api#42: Fix login", listing.Name);
        Assert.Equal(1, changes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("two\nlines")]
    public async Task RenameAsync_WithAnInvalidName_Throws(string name)
    {
        var workspace = CreateWorkspace("fix-login");
        var store = CreateStore();
        await store.SaveAsync(workspace, Token);

        await Assert.ThrowsAsync<ArgumentException>(() => store.RenameAsync(workspace.Id, name, Token));
    }

    [Fact]
    public async Task RenameAsync_UnknownWorkspace_Throws()
    {
        await Assert.ThrowsAsync<WorkspaceNotFoundException>(() => CreateStore().RenameAsync("missing", "Name", Token));
    }

    [Fact]
    public async Task RenameAsync_FolderMissing_Throws()
    {
        var workspace = CreateWorkspace("fix-login");
        var store = CreateStore();
        await store.SaveAsync(workspace, Token);
        Directory.Delete(workspace.Folder, recursive: true);

        await Assert.ThrowsAsync<WorkspaceNotFoundException>(() => store.RenameAsync(workspace.Id, "Name", Token));
    }

    [Fact]
    public async Task SaveAsync_WritesVersionedMetadataWithoutFolder()
    {
        var workspace = CreateWorkspace("Fix login");

        await CreateStore().SaveAsync(workspace, Token);

        var path = Path.Combine(workspace.Folder, ".tammi", "workspace.json");
        var json = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        Assert.Equal("version", json.First().Key);
        Assert.Equal(1, json["version"]!.GetValue<int>());
        Assert.Equal(workspace.Id, json["id"]!.GetValue<string>());
        Assert.Equal("Fix login", json["name"]!.GetValue<string>());
        Assert.Equal("github", json["tasks"]![0]!["source"]!.GetValue<string>());
        Assert.Equal("acme/api#42", json["tasks"]![0]!["key"]!.GetValue<string>());
        var repository = json["repositories"]![0]!;
        Assert.Equal("api", repository["folder"]!.GetValue<string>());
        Assert.Equal(workspace.Repositories[0].LocalRepositoryPath, repository["localRepository"]!.GetValue<string>());
        Assert.Equal("42-fix-login", repository["branch"]!.GetValue<string>());
        Assert.Equal("origin/main", repository["baseBranch"]!.GetValue<string>());
        Assert.False(json.ContainsKey("folder"));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public async Task SaveAsync_AddsWorkspaceToIndexThatANewStoreLists()
    {
        var first = CreateWorkspace("First");
        var second = CreateWorkspace("Second") with { Tasks = [], Repositories = [] };
        var store = CreateStore();

        await store.SaveAsync(first, Token);
        await store.SaveAsync(second, Token);

        var listings = await CreateStore().ListAsync(Token);
        Assert.Collection(
            listings,
            listing => AssertAvailable(first, listing),
            listing => AssertAvailable(second, listing));
    }

    [Fact]
    public async Task SaveAsync_WritesVersionedIndex()
    {
        var workspace = CreateWorkspace("Fix login");

        await CreateStore().SaveAsync(workspace, Token);

        var json = JsonNode.Parse(File.ReadAllText(IndexPath))!.AsObject();
        Assert.Equal("version", json.First().Key);
        Assert.Equal(1, json["version"]!.GetValue<int>());
        var entry = Assert.Single(json["workspaces"]!.AsArray())!;
        Assert.Equal(workspace.Id, entry["id"]!.GetValue<string>());
        Assert.Equal("Fix login", entry["name"]!.GetValue<string>());
        Assert.Equal(workspace.Folder, entry["folder"]!.GetValue<string>());
        Assert.False(File.Exists(IndexPath + ".tmp"));
    }

    [Fact]
    public async Task SaveAsync_AgainWithChanges_UpdatesEntryInPlace()
    {
        var store = CreateStore();
        var first = CreateWorkspace("First");
        var second = CreateWorkspace("Second");
        await store.SaveAsync(first, Token);
        await store.SaveAsync(second, Token);

        var renamed = first with { Name = "Renamed", Tasks = [] };
        await store.SaveAsync(renamed, Token);

        var listings = await CreateStore().ListAsync(Token);
        Assert.Collection(
            listings,
            listing => AssertAvailable(renamed, listing),
            listing => AssertAvailable(second, listing));
    }

    [Fact]
    public async Task SaveAsync_NormalizesNameAndFolder()
    {
        var workspace = CreateWorkspace("Fix login");

        var saved = await CreateStore().SaveAsync(
            workspace with { Name = "  Fix login ", Folder = workspace.Folder + Path.DirectorySeparatorChar },
            Token);

        Assert.Equal("Fix login", saved.Name);
        Assert.Equal(workspace.Folder, saved.Folder);
        Assert.Equal(workspace.Folder, Assert.Single(await CreateStore().ListAsync(Token)).Folder);
    }

    [Fact]
    public async Task SaveAsync_IntoFolderOfAnotherWorkspace_Throws()
    {
        var store = CreateStore();
        var existing = await store.SaveAsync(CreateWorkspace("Existing"), Token);

        var other = CreateWorkspace("Other") with { Folder = existing.Folder };
        var ex = await Assert.ThrowsAsync<WorkspaceException>(() => store.SaveAsync(other, Token));

        Assert.Equal(existing.Folder, ex.Folder);
        AssertSameWorkspace(existing, await CreateStore().LoadAsync(existing.Folder, Token));
    }

    [Fact]
    public async Task SaveAsync_IntoFolderWithInvalidMetadata_ReplacesIt()
    {
        var workspace = CreateWorkspace("Fix login");
        WriteMetadata(workspace.Folder, "not json");

        await CreateStore().SaveAsync(workspace, Token);

        AssertSameWorkspace(workspace, await CreateStore().LoadAsync(workspace.Folder, Token));
    }

    // Problems that can be in the metadata file. The folder isn't kept there, so it can't be invalid there.
    private static readonly string[] MetadataProblems =
    [
        "blank name",
        "blank id",
        "id with slash",
        "parent folder name",
        "folder name with separator",
        "rooted folder name",
        "metadata folder name",
        "duplicate folder names",
        "folder name with trailing space",
        "duplicate tasks",
        "task without key",
        "blank branch",
        "blank merge target",
        "relative local repository",
    ];

    public static TheoryData<string> InvalidWorkspaces() => [.. MetadataProblems, "relative folder"];

    public static TheoryData<string> InvalidMetadata() => [.. MetadataProblems];

    [Theory]
    [MemberData(nameof(InvalidWorkspaces))]
    public async Task SaveAsync_WithInvalidWorkspace_ThrowsWithoutWriting(string problem)
    {
        var workspace = MakeInvalid(CreateWorkspace("Fix login"), problem);
        var store = CreateStore();

        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(workspace, Token));

        Assert.False(File.Exists(IndexPath));
        Assert.Empty(await store.ListAsync(Token));
    }

    [Theory]
    [MemberData(nameof(InvalidMetadata))]
    public async Task LoadAsync_WithInvalidValues_Throws(string problem)
    {
        // Write valid metadata, then make it invalid in the file, as if edited by hand.
        var valid = CreateWorkspace("Fix login");
        await CreateStore().SaveAsync(valid, Token);
        WriteMetadata(valid.Folder, ToMetadataJson(MakeInvalid(valid, problem)));

        var ex = await Assert.ThrowsAsync<InvalidWorkspaceMetadataException>(
            () => CreateStore().LoadAsync(valid.Folder, Token));
        Assert.Equal(valid.Folder, ex.Folder);
    }

    [Fact]
    public async Task LoadAsync_WithMissingFolder_ThrowsNotFound()
    {
        var folder = Path.Combine(_root, "missing");

        var ex = await Assert.ThrowsAsync<WorkspaceNotFoundException>(() => CreateStore().LoadAsync(folder, Token));

        Assert.Equal(folder, ex.Folder);
    }

    [Fact]
    public async Task LoadAsync_WithFolderWithoutMetadata_ThrowsNotFound()
    {
        var folder = Path.Combine(_root, "plain");
        Directory.CreateDirectory(folder);

        await Assert.ThrowsAsync<WorkspaceNotFoundException>(() => CreateStore().LoadAsync(folder, Token));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{ "version": "one" }""")]
    [InlineData("""{ "version": 1 }""")]
    [InlineData("""{ "version": 1, "id": "abc", "name": "x", "tasks": [null] }""")]
    [InlineData("""{ "version": 1, "id": "abc", "name": "x", "repositories": "api" }""")]
    public async Task LoadAsync_WithInvalidMetadata_ThrowsInvalid(string contents)
    {
        var folder = Path.Combine(_root, "invalid");
        WriteMetadata(folder, contents);

        await Assert.ThrowsAsync<InvalidWorkspaceMetadataException>(() => CreateStore().LoadAsync(folder, Token));
    }

    [Fact]
    public async Task LoadAsync_WithMinimalMetadata_HasNoTasksOrRepositories()
    {
        var folder = Path.Combine(_root, "minimal");
        WriteMetadata(folder, """
            {
              // Written by hand
              "id": "abc",
              "name": "Minimal",
              "unknown": true,
            }
            """);

        var workspace = await CreateStore().LoadAsync(folder, Token);

        Assert.Equal("abc", workspace.Id);
        Assert.Equal("Minimal", workspace.Name);
        Assert.Equal(folder, workspace.Folder);
        Assert.Empty(workspace.Tasks);
        Assert.Empty(workspace.Repositories);
    }

    [Fact]
    public async Task LoadAsync_WithNewerVersion_ReadsKnownValues()
    {
        var folder = Path.Combine(_root, "newer");
        WriteMetadata(folder, """{ "version": 99, "id": "abc", "name": "Newer", "future": [1, 2] }""");

        var workspace = await CreateStore().LoadAsync(folder, Token);

        Assert.Equal("Newer", workspace.Name);
    }

    [Fact]
    public async Task LoadAsync_WithOlderVersion_RunsMetadataMigrations()
    {
        var folder = Path.Combine(_root, "older");
        WriteMetadata(folder, """{ "version": 1, "id": "abc", "title": "Old name" }""");
        IReadOnlyList<Action<JsonObject>> migrations =
        [
            // Version 1 to 2: renames "title" to "name".
            json =>
            {
                json["name"] = json["title"]!.GetValue<string>();
                json.Remove("title");
            },
        ];
        var store = new WorkspaceStore(DataDirectory, migrations, []);

        var workspace = await store.LoadAsync(folder, Token);

        Assert.Equal("Old name", workspace.Name);
        Assert.Equal(2, store.CurrentMetadataVersion);
    }

    [Fact]
    public async Task ListAsync_WithDeletedFolder_ListsItAsMissingByName()
    {
        var store = CreateStore();
        var deleted = await store.SaveAsync(CreateWorkspace("Deleted"), Token);
        var kept = await store.SaveAsync(CreateWorkspace("Kept"), Token);
        Directory.Delete(deleted.Folder, recursive: true);

        var listings = await CreateStore().ListAsync(Token);

        Assert.Collection(
            listings,
            listing =>
            {
                Assert.Equal(deleted.Id, listing.Id);
                Assert.Equal("Deleted", listing.Name);
                Assert.Equal(deleted.Folder, listing.Folder);
                Assert.Equal(WorkspaceAvailability.Missing, listing.Availability);
                Assert.Null(listing.Workspace);
                Assert.Contains(deleted.Folder, listing.Problem);
            },
            listing => AssertAvailable(kept, listing));
    }

    [Fact]
    public async Task FindAsync_ReturnsTheWorkspacesListing()
    {
        var store = CreateStore();
        await store.SaveAsync(CreateWorkspace("Other"), Token);
        var workspace = await store.SaveAsync(CreateWorkspace("Fix login"), Token);

        var listing = await CreateStore().FindAsync(workspace.Id, Token);

        Assert.NotNull(listing);
        AssertAvailable(workspace, listing);
    }

    [Fact]
    public async Task FindAsync_WithDeletedFolder_ReturnsItAsMissing()
    {
        var store = CreateStore();
        var workspace = await store.SaveAsync(CreateWorkspace("Fix login"), Token);
        Directory.Delete(workspace.Folder, recursive: true);

        var listing = await store.FindAsync(workspace.Id, Token);

        Assert.NotNull(listing);
        Assert.Equal(WorkspaceAvailability.Missing, listing.Availability);
        Assert.Equal("Fix login", listing.Name);
    }

    [Fact]
    public async Task FindAsync_WithUnknownId_ReturnsNull()
    {
        var store = CreateStore();
        await store.SaveAsync(CreateWorkspace("Fix login"), Token);

        Assert.Null(await store.FindAsync("unknown", Token));
    }

    [Fact]
    public async Task ListAsync_WithDeletedMetadata_ListsItAsMissing()
    {
        var store = CreateStore();
        var workspace = await store.SaveAsync(CreateWorkspace("Fix login"), Token);
        Directory.Delete(workspace.MetadataFolder, recursive: true);

        var listing = Assert.Single(await store.ListAsync(Token));

        Assert.Equal(WorkspaceAvailability.Missing, listing.Availability);
        Assert.NotNull(listing.Problem);
    }

    [Fact]
    public async Task ListAsync_WithInvalidMetadata_ListsItAsUnreadable()
    {
        var store = CreateStore();
        var workspace = await store.SaveAsync(CreateWorkspace("Fix login"), Token);
        WriteMetadata(workspace.Folder, "{ broken");

        var listing = Assert.Single(await store.ListAsync(Token));

        Assert.Equal(WorkspaceAvailability.Unreadable, listing.Availability);
        Assert.Equal("Fix login", listing.Name);
        Assert.Contains(WorkspaceStore.GetMetadataFilePath(workspace.Folder), listing.Problem);
    }

    [Fact]
    public async Task ListAsync_WhenFolderHoldsAnotherWorkspace_ListsItAsUnreadable()
    {
        var store = CreateStore();
        var workspace = await store.SaveAsync(CreateWorkspace("Original"), Token);
        Directory.Delete(workspace.Folder, recursive: true);
        await CreateStore().SaveAsync(CreateWorkspace("Replacement") with { Folder = workspace.Folder }, Token);

        var listing = Assert.Single(await store.ListAsync(Token));

        Assert.Equal(workspace.Id, listing.Id);
        Assert.Equal(WorkspaceAvailability.Unreadable, listing.Availability);
        Assert.Contains("Replacement", listing.Problem);
    }

    [Fact]
    public async Task AddExistingAsync_AfterFolderMoved_PointsEntryAtNewFolder()
    {
        var store = CreateStore();
        var moved = await store.SaveAsync(CreateWorkspace("Moved"), Token);
        var other = await store.SaveAsync(CreateWorkspace("Other"), Token);
        var newFolder = Path.Combine(_root, "elsewhere", "Moved");
        Directory.CreateDirectory(Path.GetDirectoryName(newFolder)!);
        Directory.Move(moved.Folder, newFolder);
        Assert.Equal(WorkspaceAvailability.Missing, (await store.ListAsync(Token))[0].Availability);

        var added = await store.AddExistingAsync(newFolder, Token);

        Assert.Equal(newFolder, added.Folder);
        Assert.Collection(
            await CreateStore().ListAsync(Token),
            listing => AssertAvailable(moved with { Folder = newFolder }, listing),
            listing => AssertAvailable(other, listing));
    }

    [Fact]
    public async Task AddExistingAsync_WithUnknownWorkspace_AddsIt()
    {
        var workspace = await CreateStore().SaveAsync(CreateWorkspace("Elsewhere"), Token);
        File.Delete(IndexPath);
        var store = CreateStore();

        await store.AddExistingAsync(workspace.Folder, Token);

        AssertAvailable(workspace, Assert.Single(await CreateStore().ListAsync(Token)));
    }

    [Fact]
    public async Task AddExistingAsync_WithoutWorkspace_ThrowsAndLeavesIndex()
    {
        var store = CreateStore();
        var folder = Path.Combine(_root, "plain");
        Directory.CreateDirectory(folder);

        await Assert.ThrowsAsync<WorkspaceNotFoundException>(() => store.AddExistingAsync(folder, Token));

        Assert.False(File.Exists(IndexPath));
    }

    [Fact]
    public async Task RemoveFromIndexAsync_ForgetsWorkspaceButKeepsFolder()
    {
        var store = CreateStore();
        var removed = await store.SaveAsync(CreateWorkspace("Removed"), Token);
        var kept = await store.SaveAsync(CreateWorkspace("Kept"), Token);

        Assert.True(await store.RemoveFromIndexAsync(removed.Id, Token));
        Assert.False(await store.RemoveFromIndexAsync(removed.Id, Token));

        AssertAvailable(kept, Assert.Single(await CreateStore().ListAsync(Token)));
        Assert.True(File.Exists(WorkspaceStore.GetMetadataFilePath(removed.Folder)));
    }

    [Fact]
    public async Task Changed_IsRaisedAfterEachChange()
    {
        var store = CreateStore();
        var changes = 0;
        store.Changed += () => changes++;

        var workspace = await store.SaveAsync(CreateWorkspace("Fix login"), Token);
        await store.AddExistingAsync(workspace.Folder, Token);
        await store.RemoveFromIndexAsync(workspace.Id, Token);
        await store.RemoveFromIndexAsync(workspace.Id, Token);

        Assert.Equal(3, changes);
    }

    [Fact]
    public async Task SaveAsync_ConcurrentSaves_AreAllIndexed()
    {
        var store = CreateStore();
        var workspaces = Enumerable.Range(0, 20).Select(i => CreateWorkspace($"Workspace {i}")).ToList();

        await Task.WhenAll(workspaces.Select(workspace => Task.Run(() => store.SaveAsync(workspace, Token), Token)));

        var listings = await CreateStore().ListAsync(Token);
        Assert.Equal(
            workspaces.Select(workspace => workspace.Id).Order(),
            listings.Select(listing => listing.Id).Order());
        Assert.All(listings, listing => Assert.Equal(WorkspaceAvailability.Available, listing.Availability));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{ "version": 0 }""")]
    [InlineData("""{ "version": 1, "workspaces": {} }""")]
    public async Task ListAsync_WithInvalidIndex_ListsNothingAndKeepsCopy(string contents)
    {
        WriteIndex(contents);
        var store = CreateStore();

        Assert.Empty(await store.ListAsync(Token));
        Assert.NotNull(store.IndexProblem);
        Assert.Contains(IndexPath, store.IndexProblem);
        Assert.Equal(contents, File.ReadAllText(Path.Combine(DataDirectory, WorkspaceStore.InvalidIndexFileName)));
    }

    [Fact]
    public async Task SaveAsync_AfterInvalidIndex_ReplacesItAndClearsIndexProblem()
    {
        WriteIndex("not json");
        var store = CreateStore();
        Assert.NotNull(store.IndexProblem);

        var workspace = await store.SaveAsync(CreateWorkspace("Fix login"), Token);

        Assert.Null(store.IndexProblem);
        var reloaded = CreateStore();
        Assert.Null(reloaded.IndexProblem);
        AssertAvailable(workspace, Assert.Single(await reloaded.ListAsync(Token)));
        Assert.Equal("not json", File.ReadAllText(Path.Combine(DataDirectory, WorkspaceStore.InvalidIndexFileName)));
    }

    [Fact]
    public async Task ListAsync_SkipsUnusableIndexEntries()
    {
        var workspace = await CreateStore().SaveAsync(CreateWorkspace("Fix login"), Token);
        var folder = JsonValue.Create(workspace.Folder).ToJsonString();
        WriteIndex($$"""
            {
              "version": 1,
              "workspaces": [
                null,
                { "name": "No ID", "folder": {{folder}} },
                { "id": "abc" },
                { "id": "relative", "folder": "relative/path" },
                { "id": "has space", "folder": {{folder}} },
                { "id": "{{workspace.Id}}", "folder": {{folder}} },
                { "id": "{{workspace.Id}}", "name": "Duplicate", "folder": {{folder}} },
              ],
            }
            """);
        var store = CreateStore();

        var listing = Assert.Single(await store.ListAsync(Token));

        AssertAvailable(workspace, listing);
        Assert.Null(store.IndexProblem);
    }

    [Fact]
    public async Task ListAsync_WithMissingFolderAndNoName_UsesFolderName()
    {
        var folder = JsonValue.Create(Path.Combine(_root, "gone", "Old workspace")).ToJsonString();
        WriteIndex($$"""{ "version": 1, "workspaces": [{ "id": "abc", "folder": {{folder}} }] }""");

        var listing = Assert.Single(await CreateStore().ListAsync(Token));

        Assert.Equal("Old workspace", listing.Name);
        Assert.Equal(WorkspaceAvailability.Missing, listing.Availability);
    }

    [Fact]
    public async Task ListAsync_WithOlderIndexVersion_RunsIndexMigrations()
    {
        var workspace = await CreateStore().SaveAsync(CreateWorkspace("Fix login"), Token);
        var folder = JsonValue.Create(workspace.Folder).ToJsonString();
        WriteIndex($$"""{ "version": 1, "paths": [{ "id": "{{workspace.Id}}", "path": {{folder}} }] }""");
        IReadOnlyList<Action<JsonObject>> migrations =
        [
            // Version 1 to 2: renames "paths" to "workspaces", and "path" to "folder".
            json =>
            {
                var entries = json["paths"]!.AsArray();
                json.Remove("paths");
                foreach (var entry in entries)
                {
                    entry!["folder"] = entry["path"]!.GetValue<string>();
                }

                json["workspaces"] = entries;
            },
        ];
        var store = new WorkspaceStore(DataDirectory, [], migrations);

        AssertAvailable(workspace, Assert.Single(await store.ListAsync(Token)));
        Assert.Equal(2, store.CurrentIndexVersion);
    }

    [Fact]
    public void GetWorktreePath_CombinesFolderAndFolderName()
    {
        var workspace = CreateWorkspace("Fix login");

        Assert.Equal(Path.Combine(workspace.Folder, "api"), workspace.GetWorktreePath(workspace.Repositories[0]));
        Assert.Equal(Path.Combine(workspace.Folder, ".tammi"), workspace.MetadataFolder);
    }

    [Fact]
    public void NewId_IsUnique()
    {
        Assert.NotEqual(Workspace.NewId(), Workspace.NewId());
    }

    private WorkspaceStore CreateStore() => new(DataDirectory, [], []);

    private Workspace CreateWorkspace(string name) => new()
    {
        Id = Workspace.NewId(),
        Name = name,
        Folder = Path.Combine(_root, "workspaces", name),
        Tasks = [new TaskReference("github", "acme/api#42"), new TaskReference("jira", "ABC-123")],
        Repositories =
        [
            new WorkspaceRepository("api", Path.Combine(_root, "clones", "api"), "42-fix-login", "origin/main"),
            new WorkspaceRepository(
                "acme-web",
                Path.Combine(_root, "clones", "web"),
                "42-fix-login",
                "upstream/develop"),
        ],
    };

    private static Workspace MakeInvalid(Workspace workspace, string problem)
    {
        var repository = workspace.Repositories[0];
        return problem switch
        {
            "blank name" => workspace with { Name = " " },
            "relative folder" => workspace with { Folder = "relative/folder" },
            "blank id" => workspace with { Id = "" },
            "id with slash" => workspace with { Id = "a/b" },
            "parent folder name" => WithRepository(workspace, repository with { FolderName = ".." }),
            "folder name with separator" => WithRepository(workspace, repository with { FolderName = "../api" }),
            "rooted folder name" => WithRepository(
                workspace,
                repository with { FolderName = Path.Combine(Path.GetTempPath(), "api") }),
            "metadata folder name" => WithRepository(workspace, repository with { FolderName = ".TAMMI" }),
            "duplicate folder names" => workspace with { Repositories = [repository, repository] },
            "folder name with trailing space" => WithRepository(workspace, repository with { FolderName = "api " }),
            "duplicate tasks" => workspace with { Tasks = [workspace.Tasks[0], workspace.Tasks[0]] },
            "task without key" => workspace with { Tasks = [new TaskReference("github", " ")] },
            "blank branch" => WithRepository(workspace, repository with { Branch = "" }),
            "blank merge target" => WithRepository(workspace, repository with { MergeTarget = " " }),
            "relative local repository" => WithRepository(workspace, repository with { LocalRepositoryPath = "api" }),
            _ => throw new ArgumentOutOfRangeException(nameof(problem), problem, null),
        };
    }

    private static Workspace WithRepository(Workspace workspace, WorkspaceRepository repository) =>
        workspace with { Repositories = [repository] };

    private static string ToMetadataJson(Workspace workspace) => new JsonObject
    {
        ["version"] = 1,
        ["id"] = workspace.Id,
        ["name"] = workspace.Name,
        ["tasks"] = new JsonArray([.. workspace.Tasks.Select(task => new JsonObject
        {
            ["source"] = task.Source,
            ["key"] = task.Key,
        })]),
        ["repositories"] = new JsonArray([.. workspace.Repositories.Select(repository => new JsonObject
        {
            ["folder"] = repository.FolderName,
            ["localRepository"] = repository.LocalRepositoryPath,
            ["branch"] = repository.Branch,
            ["baseBranch"] = repository.MergeTarget,
        })]),
    }.ToJsonString();

    private static void WriteMetadata(string folder, string contents)
    {
        var path = WorkspaceStore.GetMetadataFilePath(folder);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
    }

    private void WriteIndex(string contents)
    {
        Directory.CreateDirectory(DataDirectory);
        File.WriteAllText(IndexPath, contents);
    }

    private static void AssertAvailable(Workspace expected, WorkspaceListing listing)
    {
        Assert.Equal(expected.Id, listing.Id);
        Assert.Equal(expected.Name, listing.Name);
        Assert.Equal(expected.Folder, listing.Folder);
        Assert.Equal(WorkspaceAvailability.Available, listing.Availability);
        Assert.Null(listing.Problem);
        AssertSameWorkspace(expected, listing.Workspace!);
    }

    private static void AssertSameWorkspace(Workspace expected, Workspace actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Folder, actual.Folder);
        Assert.Equal(expected.Tasks, actual.Tasks);
        Assert.Equal(expected.Repositories, actual.Repositories);
    }

    private sealed class TestAppPaths(string dataDirectory) : IAppPaths
    {
        public string ConfigDirectory => throw new NotSupportedException();

        public string DataDirectory => dataDirectory;

        public string CacheDirectory => throw new NotSupportedException();

        public string LogDirectory => throw new NotSupportedException();
    }
}
