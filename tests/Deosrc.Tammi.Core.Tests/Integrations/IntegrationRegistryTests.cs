using TAMMi.Core.Integrations;
using TAMMi.Core.Tests.Credentials;

namespace TAMMi.Core.Tests.Integrations;

public class IntegrationRegistryTests
{
    private readonly InMemoryCredentialStore _store = new();

    [Fact]
    public void Integrations_AreListedByWhatTheyProvide()
    {
        var both = new FakeIntegration("github", _store);
        var tasksOnly = new FakeTaskOnlyIntegration("jira");

        var registry = new IntegrationRegistry([both, tasksOnly]);

        Assert.Equal([both, tasksOnly], registry.All);
        Assert.Equal([both, tasksOnly], registry.TaskIntegrations);
        Assert.Equal([both], registry.RepositoryIntegrations);
    }

    [Fact]
    public void Find_ReturnsIntegrationById()
    {
        var github = new FakeIntegration("github", _store);
        var registry = new IntegrationRegistry([github, new FakeTaskOnlyIntegration("jira")]);

        Assert.Same(github, registry.Find("github"));
        Assert.Null(registry.Find("GitHub"));
        Assert.Null(registry.Find("gitlab"));
    }

    [Fact]
    public void Add_Throws_WhenIdIsTaken()
    {
        var registry = new IntegrationRegistry([new FakeIntegration("jira", _store)]);

        Assert.Throws<ArgumentException>(() => registry.Add(new FakeTaskOnlyIntegration("jira")));
        Assert.Throws<ArgumentException>(
            () => new IntegrationRegistry([new FakeTaskOnlyIntegration("a"), new FakeTaskOnlyIntegration("a")]));
    }

    [Fact]
    public void AddAndRemove_UpdateTheListsAndRaiseChanged()
    {
        var registry = new IntegrationRegistry([]);
        int changes = 0;
        registry.Changed += (_, _) => changes++;

        registry.Add(new FakeIntegration("github", _store));
        registry.Add(new FakeTaskOnlyIntegration("jira"));
        IReadOnlyList<IIntegration> before = registry.All;

        Assert.True(registry.Remove("github"));
        Assert.False(registry.Remove("github"));

        Assert.Equal(3, changes);
        Assert.Equal(["jira"], registry.All.Select(integration => integration.Id));
        Assert.Empty(registry.RepositoryIntegrations);
        Assert.Equal(2, before.Count);
    }

    [Fact]
    public void Find_ReturnsNull_ForATaskWhoseIntegrationWasRemoved()
    {
        var registry = new IntegrationRegistry([new FakeIntegration("github", _store)]);
        var task = new TaskInfo("github", "acme/api#1", "Title", new Uri("https://fake.example/1"));

        registry.Remove("github");

        Assert.Null(registry.Find(task.Source));
    }
}
