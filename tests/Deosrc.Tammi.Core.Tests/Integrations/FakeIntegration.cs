using System.Diagnostics.CodeAnalysis;
using Deosrc.Tammi.Core.Credentials;
using Deosrc.Tammi.Core.Integrations;

namespace Deosrc.Tammi.Core.Tests.Integrations;

/// <summary>
/// An integration that provides tasks and repositories from memory. Like a real integration, it needs a token in the
/// credential store and rejects a wrong one.
/// </summary>
internal sealed class FakeIntegration(string id, ICredentialStore store) : ITaskIntegration, IRepositoryIntegration
{
    public const string ValidToken = "valid-token";

    private readonly IntegrationCredentials _credentials = new(store, id);

    public string Id { get; } = id;

    public string Name => $"Fake ({Id})";

    public List<(string Key, string Title)> Tasks { get; } = [];

    public List<(string Owner, string Name)> Repositories { get; } = [];

    public async Task<IReadOnlyList<TaskInfo>> GetAssignedTasksAsync(CancellationToken cancellationToken = default)
    {
        await AuthenticateAsync(cancellationToken);
        return [.. Tasks.Select(ToTask)];
    }

    public async Task<IReadOnlyList<TaskInfo>> SearchTasksAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        await AuthenticateAsync(cancellationToken);
        return [.. Tasks.Where(task => task.Title.Contains(text, StringComparison.Ordinal)).Select(ToTask)];
    }

    /// <summary>
    /// Reads a key of one of <see cref="Tasks"/>, or a task's URL.
    /// </summary>
    public bool TryParseTaskReference(string? text, [NotNullWhen(true)] out string? key)
    {
        const string urlPrefix = "https://fake.example/tasks/";
        text = text?.Trim();
        if (text?.StartsWith(urlPrefix, StringComparison.Ordinal) == true)
        {
            text = text[urlPrefix.Length..];
        }

        key = Tasks.Any(task => task.Key == text) ? text : null;
        return key is not null;
    }

    public async Task<TaskInfo?> GetTaskAsync(string key, CancellationToken cancellationToken = default)
    {
        await AuthenticateAsync(cancellationToken);
        return Tasks.Where(task => task.Key == key).Select(ToTask).FirstOrDefault();
    }

    public async Task<IReadOnlyList<IntegrationRepository>> GetRepositoriesAsync(
        CancellationToken cancellationToken = default)
    {
        await AuthenticateAsync(cancellationToken);
        return
        [
            .. Repositories.Select(repository => new IntegrationRepository(
                Id,
                repository.Owner,
                repository.Name,
                [
                    $"https://fake.example/{repository.Owner}/{repository.Name}.git",
                    $"git@fake.example:{repository.Owner}/{repository.Name}.git",
                ])),
        ];
    }

    private async Task AuthenticateAsync(CancellationToken cancellationToken)
    {
        string token = await _credentials.GetRequiredAsync(cancellationToken);
        if (token != ValidToken)
        {
            throw new IntegrationAuthenticationException(Id, "The token was rejected.");
        }
    }

    private TaskInfo ToTask((string Key, string Title) task) =>
        new(Id, task.Key, task.Title, new Uri($"https://fake.example/tasks/{task.Key}"))
        {
            Description = $"Description of {task.Key}",
        };
}
