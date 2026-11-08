using System.Globalization;
using Deosrc.Tammi.Core.Platform;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Deosrc.Tammi.Core.Ssh;

/// <summary>
/// The SSH keys in a folder, generated with <c>ssh-keygen</c>.
/// </summary>
/// <param name="processes">Finds and runs <c>ssh-keygen</c>.</param>
/// <param name="keyDirectory">The SSH folder. It defaults to <c>.ssh</c> in the user's home folder.</param>
/// <param name="logger">Logs the keys generated and removed. Passphrases are never logged.</param>
public sealed class SshKeyStore(
    IProcessLauncher processes,
    string? keyDirectory = null,
    ILogger<SshKeyStore>? logger = null) : ISshKeyStore
{
    private const string PublicKeyExtension = ".pub";

    private readonly ILogger _logger = logger ?? NullLogger<SshKeyStore>.Instance;
    private readonly Lazy<string?> _keygen = new(() => FindKeygen(processes));

    /// <inheritdoc />
    public string KeyDirectory { get; } = keyDirectory
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");

    /// <inheritdoc />
    public bool CanGenerate => _keygen.Value is not null;

    /// <inheritdoc />
    public async Task<IReadOnlyList<SshKey>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(KeyDirectory))
        {
            return [];
        }

        var keys = new List<SshKey>();
        foreach (string publicKeyPath in Directory.EnumerateFiles(KeyDirectory, "*" + PublicKeyExtension))
        {
            string privateKeyPath = publicKeyPath[..^PublicKeyExtension.Length];
            if (!File.Exists(privateKeyPath))
            {
                continue;
            }

            if (await ReadAsync(privateKeyPath, cancellationToken).ConfigureAwait(false) is { } key)
            {
                keys.Add(key);
            }
        }

        return [.. keys.OrderBy(key => key.Name, StringComparer.OrdinalIgnoreCase)];
    }

    /// <inheritdoc />
    public string SuggestName(SshKeyType type)
    {
        string baseName = type == SshKeyType.Rsa ? "id_rsa" : "id_ed25519";
        string name = baseName;
        for (int number = 2; IsTaken(name); number++)
        {
            name = string.Create(CultureInfo.InvariantCulture, $"{baseName}_{number}");
        }

        return name;
    }

    /// <inheritdoc />
    public string? CheckName(string? name)
    {
        name = name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return "Enter a name for the key.";
        }

        if (name is "." or ".."
            || name.IndexOfAny(['/', '\\']) >= 0
            || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return "The name can't contain a folder, or characters that aren't allowed in file names.";
        }

        if (name.EndsWith(PublicKeyExtension, StringComparison.OrdinalIgnoreCase))
        {
            return $"The name can't end with '{PublicKeyExtension}'. That's added to the public key's file name.";
        }

        return IsTaken(name) ? $"There's already a file called '{name}' in {KeyDirectory}." : null;
    }

    /// <inheritdoc />
    public async Task<SshKey> GenerateAsync(SshKeyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string name = request.Name.Trim();
        if (CheckName(name) is { } problem)
        {
            throw new ArgumentException(problem, nameof(request));
        }

        string keygen = _keygen.Value ?? throw new SshKeygenNotFoundException();
        CreateKeyDirectory();

        string privateKeyPath = Path.Combine(KeyDirectory, name);
        string[] arguments =
        [
            "-t", request.Type == SshKeyType.Rsa ? "rsa" : "ed25519",
            .. request.Type == SshKeyType.Rsa ? ["-b", "4096"] : Array.Empty<string>(),
            "-C", request.Comment?.Trim() ?? "",
            // ssh-keygen can't be given the passphrase on standard input, and its standard input is closed, so it's
            // passed as an argument. It's never logged.
            "-N", request.Passphrase ?? "",
            "-f", privateKeyPath,
            "-q",
        ];

        ProcessResult result;
        try
        {
            result = await processes
                .RunAsync(new ProcessRequest(keygen, arguments) { WorkingDirectory = KeyDirectory }, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (FileNotFoundException exception)
        {
            throw new SshKeyException("Couldn't run ssh-keygen. " + exception.Message, exception);
        }

        if (!result.Succeeded)
        {
            string error = (result.StandardError.Trim() is { Length: > 0 } stderr ? stderr : result.StandardOutput.Trim());
            _logger.LogWarning("ssh-keygen failed with exit code {ExitCode}: {Error}", result.ExitCode, error);
            throw new SshKeyException(error.Length == 0
                ? $"ssh-keygen failed with exit code {result.ExitCode}."
                : $"ssh-keygen failed: {error}");
        }

        SshKey key = await ReadAsync(privateKeyPath, cancellationToken).ConfigureAwait(false)
            ?? throw new SshKeyException($"ssh-keygen didn't write a readable public key to {privateKeyPath}{PublicKeyExtension}.");
        _logger.LogInformation("Generated the SSH key {Path} ({Fingerprint}).", privateKeyPath, key.Fingerprint);
        return key;
    }

    /// <inheritdoc />
    public Task RemoveAsync(SshKey key, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        cancellationToken.ThrowIfCancellationRequested();

        File.Delete(key.PrivateKeyPath);
        File.Delete(key.PublicKeyPath);
        _logger.LogInformation("Removed the SSH key {Path} ({Fingerprint}).", key.PrivateKeyPath, key.Fingerprint);
        return Task.CompletedTask;
    }

    private bool IsTaken(string name)
    {
        string path = Path.Combine(KeyDirectory, name);
        return File.Exists(path) || File.Exists(path + PublicKeyExtension) || Directory.Exists(path);
    }

    private void CreateKeyDirectory()
    {
        if (Directory.Exists(KeyDirectory))
        {
            return;
        }

        // OpenSSH refuses keys in a folder others can write to, so it's created for the user only.
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(KeyDirectory);
        }
        else
        {
            Directory.CreateDirectory(KeyDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    private async Task<SshKey?> ReadAsync(string privateKeyPath, CancellationToken cancellationToken)
    {
        string line;
        try
        {
            using var reader = new StreamReader(privateKeyPath + PublicKeyExtension);
            line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) ?? "";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning("Couldn't read the public key {Path}: {Message}", privateKeyPath + PublicKeyExtension,
                exception.Message);
            return null;
        }

        return SshKey.TryParsePublicKey(line, out string? algorithm, out string? data, out string? comment)
            ? new SshKey(Path.GetFileName(privateKeyPath), privateKeyPath, algorithm, data, comment)
            : null;
    }

    /// <summary>
    /// Finds <c>ssh-keygen</c> on the path. On Windows, it falls back to the one that comes with Git for Windows,
    /// which is often not on the path.
    /// </summary>
    private static string? FindKeygen(IProcessLauncher processes)
    {
        if (processes.FindExecutable("ssh-keygen") is { } keygen)
        {
            return keygen;
        }

        if (!OperatingSystem.IsWindows() || processes.FindExecutable("git") is not { } git)
        {
            return null;
        }

        // git.exe is in <install>\cmd, <install>\bin or <install>\mingw64\bin, and ssh-keygen in <install>\usr\bin.
        for (DirectoryInfo? folder = Directory.GetParent(git)?.Parent; folder is not null; folder = folder.Parent)
        {
            string candidate = Path.Combine(folder.FullName, "usr", "bin", "ssh-keygen.exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
