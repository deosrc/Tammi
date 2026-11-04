using Tammi.Core.Git;

namespace Tammi.Core.Tests.Git;

/// <summary>
/// A temporary folder for building git repositories in tests. Git runs with its own global config, so the
/// developer's or CI runner's settings (signing, hooks, default branch) can't affect the tests.
/// </summary>
internal sealed class TempRepositories : IDisposable
{
    private const string GlobalConfig = """
        [user]
            name = Tammi Tests
            email = tests@tammi.invalid
        [init]
            defaultBranch = main
        [commit]
            gpgSign = false
        [tag]
            gpgSign = false
        [core]
            autocrlf = false
            longpaths = true
        [protocol "file"]
            allow = always
        """;

    public TempRepositories()
    {
        Root = Path.Combine(Path.GetTempPath(), "tammi-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);

        string globalConfigPath = Path.Combine(Root, ".gitconfig");
        File.WriteAllText(globalConfigPath, GlobalConfig);

        Git = new GitCli(new GitCliOptions
        {
            Environment = new Dictionary<string, string?>
            {
                ["GIT_CONFIG_GLOBAL"] = globalConfigPath,
                ["GIT_CONFIG_NOSYSTEM"] = "1",
            },
        });
    }

    public string Root { get; }

    public GitCli Git { get; }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    /// <summary>
    /// Creates a bare repository to act as a remote, with an initial commit on <c>main</c>.
    /// </summary>
    public async Task<string> CreateRemoteAsync(string name)
    {
        string remote = PathTo($"{name}.git");
        await RunAsync(Root, "init", "--bare", remote);

        string seed = PathTo($"{name}-seed");
        await RunAsync(Root, "clone", "--config", "core.autocrlf=false", remote, seed);
        await CommitFileAsync(seed, "README.md", $"# {name}\n", "Initial commit");
        await RunAsync(seed, "push", "origin", "main");
        return remote;
    }

    /// <summary>
    /// Creates a bare repository <c>app</c> whose <c>main</c> has the remote <c>lib</c> as a submodule at <c>lib</c>.
    /// With <paramref name="nested"/>, <c>lib</c> has the remote <c>core</c> as a submodule at <c>core</c>.
    /// </summary>
    public async Task<string> CreateRemoteWithSubmoduleAsync(bool nested = false)
    {
        string libRemote = await CreateRemoteAsync("lib");
        if (nested)
        {
            string coreRemote = await CreateRemoteAsync("core");
            string libSeed = PathTo("lib-seed");
            await RunAsync(libSeed, "submodule", "add", coreRemote, "core");
            await RunAsync(libSeed, "commit", "-m", "Add core submodule");
            await RunAsync(libSeed, "push");
        }

        string appRemote = await CreateRemoteAsync("app");
        string appSeed = PathTo("app-seed");
        await RunAsync(appSeed, "submodule", "add", libRemote, "lib");
        await RunAsync(appSeed, "commit", "-m", "Add lib submodule");
        await RunAsync(appSeed, "push");
        return appRemote;
    }

    /// <summary>
    /// Clones <paramref name="remote"/> into a new folder and returns its path.
    /// </summary>
    public async Task<string> CloneAsync(string remote, string name, params string[] extraArguments)
    {
        string path = PathTo(name);
        // Set autocrlf in the repository too, so LibGit2Sharp (which reads the real global and system config)
        // sees the same line ending settings as the git CLI.
        await RunAsync(Root, ["clone", "--config", "core.autocrlf=false", .. extraArguments, remote, path]);
        return path;
    }

    public async Task CommitFileAsync(string repository, string file, string content, string message)
    {
        WriteFile(repository, file, content);
        await RunAsync(repository, "add", "--", file);
        await RunAsync(repository, "commit", "-m", message);
    }

    public Task<GitCommandResult> RunAsync(string workingDirectory, params string[] arguments) =>
        Git.RunAsync(workingDirectory, arguments, cancellationToken: CancellationToken);

    public static void WriteFile(string repository, string file, string content)
    {
        string path = Path.Combine(repository, file);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    /// <summary>
    /// The full path to <paramref name="name"/> in the folder, with the platform's separators, so tests can write
    /// <c>/</c> in names and still compare paths with the ones Tammi builds.
    /// </summary>
    public string PathTo(string name) => Path.GetFullPath(Path.Combine(Root, name));

    /// <summary>
    /// Deletes a folder holding git files.
    /// </summary>
    public static void DeleteFolder(string path)
    {
        // Git makes object files read-only, which stops Directory.Delete on Windows. Links aren't followed, because a
        // link back up to a parent folder would never end.
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
        };
        foreach (string file in Directory.EnumerateFiles(path, "*", options))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(path, recursive: true);
    }

    public void Dispose() => DeleteFolder(Root);
}
