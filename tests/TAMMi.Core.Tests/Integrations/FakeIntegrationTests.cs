using TAMMi.Core.Integrations;
using TAMMi.Core.Tests.Credentials;

namespace TAMMi.Core.Tests.Integrations;

public class FakeIntegrationTests
{
    private readonly InMemoryCredentialStore _store = new();
    private readonly FakeIntegration _integration;

    public FakeIntegrationTests()
    {
        _integration = new FakeIntegration("fake-1", _store)
        {
            Tasks = { ("FAKE-1", "Fix the login page"), ("FAKE-2", "Add dark mode") },
            Repositories = { ("acme", "api"), ("acme", "web") },
        };
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Calls_ThrowAuthenticationException_WhenNotAuthenticated()
    {
        var exception = await Assert.ThrowsAsync<IntegrationAuthenticationException>(
            () => _integration.SearchTasksAsync(null, CancellationToken));

        Assert.Equal("fake-1", exception.IntegrationId);
        Assert.Contains("Settings", exception.Message);
    }

    [Fact]
    public async Task Calls_ThrowAuthenticationException_WhenCredentialsAreRejected()
    {
        await new IntegrationCredentials(_store, "fake-1").SaveAsync("wrong", CancellationToken);

        await Assert.ThrowsAsync<IntegrationAuthenticationException>(
            () => _integration.GetRepositoriesAsync(CancellationToken));
    }

    [Fact]
    public async Task SearchTasksAsync_ReturnsTasksFromItsSource()
    {
        await AuthenticateAsync();

        IReadOnlyList<TaskInfo> all = await _integration.SearchTasksAsync(null, CancellationToken);
        IReadOnlyList<TaskInfo> found = await _integration.SearchTasksAsync("dark", CancellationToken);

        Assert.Equal(["FAKE-1", "FAKE-2"], all.Select(task => task.Key));
        TaskInfo task = Assert.Single(found);
        Assert.Equal(new TaskReference("fake-1", "FAKE-2"), task.Reference);
        Assert.Equal("Add dark mode", task.Title);
        Assert.Equal("Description of FAKE-2", task.Description);
        Assert.Equal(new Uri("https://fake.example/tasks/FAKE-2"), task.Url);
    }

    [Fact]
    public async Task GetTaskAsync_FindsTaskByKey()
    {
        await AuthenticateAsync();

        TaskInfo? task = await _integration.GetTaskAsync("FAKE-1", CancellationToken);
        TaskInfo? missing = await _integration.GetTaskAsync("FAKE-99", CancellationToken);

        Assert.Equal("Fix the login page", task?.Title);
        Assert.Null(missing);
    }

    [Fact]
    public async Task GetRepositoriesAsync_ReturnsRepositoriesWithCloneUrls()
    {
        await AuthenticateAsync();

        IReadOnlyList<IntegrationRepository> repositories = await _integration.GetRepositoriesAsync(CancellationToken);

        Assert.Equal(["acme/api", "acme/web"], repositories.Select(repository => repository.FullName));
        IntegrationRepository api = repositories[0];
        Assert.Equal("fake-1", api.Source);
        Assert.Equal(["https://fake.example/acme/api.git", "git@fake.example:acme/api.git"], api.CloneUrls);
    }

    [Fact]
    public void TaskReference_IsEqualForTheSameSourceAndKey()
    {
        var first = new TaskInfo("fake-1", "FAKE-1", "Old title", new Uri("https://fake.example/1"));
        TaskInfo renamed = first with { Title = "New title" };

        Assert.Equal(first.Reference, renamed.Reference);
        Assert.NotEqual(first.Reference, new TaskReference("fake-2", "FAKE-1"));
        Assert.Equal("fake-1:FAKE-1", first.Reference.ToString());
    }

    private Task AuthenticateAsync() =>
        new IntegrationCredentials(_store, "fake-1").SaveAsync(FakeIntegration.ValidToken, CancellationToken);
}
