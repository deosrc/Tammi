using Tammi.Core.Catalogue;
using Tammi.Core.Tests.Git;
using Tammi.Core.Workspaces;

namespace Tammi.Core.Tests.Catalogue;

public sealed class RepositoryScannerTests : IDisposable
{
    private readonly TempRepositories _repos = new();

    public void Dispose() => _repos.Dispose();

    private string Monitored => _repos.PathTo("monitored");

    [Fact]
    public async Task FindRepositories_FindsRepositoriesAtEachLevel()
    {
        string top = await InitAsync("api");
        string nested = await InitAsync(Path.Combine("github", "acme", "web"));
        string sibling = await InitAsync(Path.Combine("github", "acme", "worker"));

        var found = RepositoryScanner.FindRepositories(Monitored, TestContext.Current.CancellationToken);

        Assert.Equal([top, nested, sibling], found);
    }

    [Fact]
    public async Task FindRepositories_IgnoresFoldersThatAreNotRepositories()
    {
        string repository = await InitAsync(Path.Combine("work", "api"));
        Directory.CreateDirectory(Path.Combine(Monitored, "empty"));
        TempRepositories.WriteFile(Path.Combine(Monitored, "notes"), "todo.txt", "Nothing to see");
        TempRepositories.WriteFile(Path.Combine(Monitored, "work", "docs"), "README.md", "# Docs");
        TempRepositories.WriteFile(Monitored, "loose-file.txt", "Not a folder");

        var found = RepositoryScanner.FindRepositories(Monitored, TestContext.Current.CancellationToken);

        Assert.Equal([repository], found);
    }

    [Fact]
    public async Task FindRepositories_DoesNotLookInsideRepositories()
    {
        string appRemote = await _repos.CreateRemoteWithSubmoduleAsync();
        string app = await _repos.CloneAsync(appRemote, Path.Combine("monitored", "app"), "--recurse-submodules");
        // A repository nested in another, e.g. one cloned into it by a build script.
        await _repos.RunAsync(app, "init", Path.Combine(app, "vendor", "tool"));

        var found = RepositoryScanner.FindRepositories(Monitored, TestContext.Current.CancellationToken);

        Assert.Equal([app], found);
    }

    [Fact]
    public async Task FindRepositories_SkipsLinkedWorktreesAndWorkspaceFolders()
    {
        string api = await InitWithCommitAsync("api");
        await _repos.RunAsync(api, "worktree", "add", "-b", "feature", Path.Combine(Monitored, "api-feature"));
        string workspace = Path.Combine(Monitored, "workspaces", "fix-login");
        Directory.CreateDirectory(Path.Combine(workspace, Workspace.MetadataFolderName));
        await _repos.RunAsync(api, "worktree", "add", "-b", "fix-login", Path.Combine(workspace, "api"));
        // A full clone in a Workspace Folder isn't something Tammi makes, so it isn't looked for either.
        await _repos.RunAsync(_repos.Root, "init", Path.Combine(workspace, "stray"));

        var found = RepositoryScanner.FindRepositories(Monitored, TestContext.Current.CancellationToken);

        Assert.Equal([api], found);
    }

    [Fact]
    public async Task FindRepositories_FindsRepositoryWithSeparateGitFolder()
    {
        string path = Path.Combine(Monitored, "separate");
        await _repos.RunAsync(_repos.Root, "init", "--separate-git-dir", _repos.PathTo("separate.git"), path);

        var found = RepositoryScanner.FindRepositories(Monitored, TestContext.Current.CancellationToken);

        Assert.Equal([path], found);
        Assert.True(File.Exists(Path.Combine(path, ".git")));
    }

    [Fact]
    public async Task FindRepositories_StopsAtMaxDepth()
    {
        string[] atMaxDepth = [.. Enumerable.Range(1, RepositoryScanner.MaxDepth).Select(i => $"level{i}")];
        string deepest = await InitAsync(Path.Combine(atMaxDepth));
        await InitAsync(Path.Combine([.. atMaxDepth[..^1], "below", "too-deep"]));

        var found = RepositoryScanner.FindRepositories(Monitored, TestContext.Current.CancellationToken);

        Assert.Equal([deepest], found);
    }

    [Fact]
    public async Task FindRepositories_SkipsHiddenFolders()
    {
        string visible = await InitAsync("visible");
        string hiddenParent = Path.Combine(Monitored, ".cache");
        await InitAsync(Path.Combine(".cache", "hidden"));
        if (OperatingSystem.IsWindows())
        {
            File.SetAttributes(hiddenParent, File.GetAttributes(hiddenParent) | FileAttributes.Hidden);
        }

        var found = RepositoryScanner.FindRepositories(Monitored, TestContext.Current.CancellationToken);

        Assert.Equal([visible], found);
    }

    [Fact]
    public async Task FindRepositories_WhenFolderIsARepository_ListsOnlyIt()
    {
        await _repos.RunAsync(_repos.Root, "init", Monitored);
        await InitAsync("nested");

        var found = RepositoryScanner.FindRepositories(Monitored, TestContext.Current.CancellationToken);

        Assert.Equal([Monitored], found);
    }

    [Fact]
    public async Task FindRepositories_DoesNotFollowLinksToFolders()
    {
        string api = await InitAsync("api");
        string elsewhere = _repos.PathTo("elsewhere");
        await _repos.RunAsync(_repos.Root, "init", Path.Combine(elsewhere, "web"));
        try
        {
            Directory.CreateSymbolicLink(Path.Combine(Monitored, "link"), elsewhere);
            // A link back up would loop forever if it were followed.
            Directory.CreateSymbolicLink(Path.Combine(Monitored, "loop"), Monitored);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Windows only lets administrators and Developer Mode create symbolic links.
            Assert.Skip($"Couldn't create a symbolic link. {ex.Message}");
        }

        var found = RepositoryScanner.FindRepositories(Monitored, TestContext.Current.CancellationToken);

        Assert.Equal([api], found);
    }

    [Fact]
    public void FindRepositories_WithMissingFolder_FindsNothing()
    {
        Assert.Empty(RepositoryScanner.FindRepositories(_repos.PathTo("missing"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task FindRepositories_WhenCancelled_Throws()
    {
        await InitAsync("api");

        Assert.Throws<OperationCanceledException>(
            () => RepositoryScanner.FindRepositories(Monitored, new CancellationToken(canceled: true)));
    }

    [Fact]
    public async Task Inspect_TellsRepositoriesWorktreesAndOtherFolders()
    {
        string api = await InitWithCommitAsync("api");
        string worktree = _repos.PathTo("api-feature");
        await _repos.RunAsync(api, "worktree", "add", "-b", "feature", worktree);
        string plain = _repos.PathTo("plain");
        Directory.CreateDirectory(plain);

        Assert.Equal(RepositoryFolderKind.Repository, RepositoryScanner.Inspect(api));
        Assert.Equal(RepositoryFolderKind.LinkedWorktree, RepositoryScanner.Inspect(worktree));
        Assert.Equal(RepositoryFolderKind.NotRepository, RepositoryScanner.Inspect(plain));
        Assert.Equal(RepositoryFolderKind.NotRepository, RepositoryScanner.Inspect(Path.Combine(api, "sub")));
    }

    private async Task<string> InitAsync(string relativePath)
    {
        string path = Path.Combine(Monitored, relativePath);
        await _repos.RunAsync(_repos.Root, "init", path);
        return path;
    }

    private async Task<string> InitWithCommitAsync(string relativePath)
    {
        string path = await InitAsync(relativePath);
        await _repos.CommitFileAsync(path, "README.md", "# Test\n", "Initial commit");
        return path;
    }
}
