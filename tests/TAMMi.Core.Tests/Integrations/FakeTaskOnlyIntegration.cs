using TAMMi.Core.Integrations;

namespace TAMMi.Core.Tests.Integrations;

/// <summary>
/// An integration that only provides tasks, e.g. JIRA.
/// </summary>
internal sealed class FakeTaskOnlyIntegration(string id) : ITaskIntegration
{
    public string Id { get; } = id;

    public string Name => "Tasks only";

    public Task<IReadOnlyList<TaskInfo>> SearchTasksAsync(
        string? query,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TaskInfo>>([]);

    public Task<TaskInfo?> GetTaskAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult<TaskInfo?>(null);
}
