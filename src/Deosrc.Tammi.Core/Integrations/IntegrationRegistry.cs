namespace Deosrc.Tammi.Core.Integrations;

/// <summary>
/// The integrations Tammi is connected to. Settings adds and removes them; the Create Workspace flow and the
/// Repository Catalogue read them.
/// </summary>
/// <remarks>
/// Safe to use from any thread. The lists are snapshots: they don't change after they are returned.
/// </remarks>
public sealed class IntegrationRegistry
{
    private readonly Lock _lock = new();
    private IReadOnlyList<IIntegration> _integrations = [];

    /// <param name="integrations">The integrations to start with.</param>
    /// <exception cref="ArgumentException">Two integrations have the same ID.</exception>
    public IntegrationRegistry(IEnumerable<IIntegration> integrations)
    {
        ArgumentNullException.ThrowIfNull(integrations);
        foreach (IIntegration integration in integrations)
        {
            Add(integration);
        }
    }

    /// <summary>
    /// Raised after an integration is added or removed. It can be raised on any thread.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// All integrations, in the order they were added.
    /// </summary>
    public IReadOnlyList<IIntegration> All => _integrations;

    /// <summary>
    /// The integrations that provide tasks.
    /// </summary>
    public IReadOnlyList<ITaskIntegration> TaskIntegrations => [.. _integrations.OfType<ITaskIntegration>()];

    /// <summary>
    /// The integrations that provide repositories.
    /// </summary>
    public IReadOnlyList<IRepositoryIntegration> RepositoryIntegrations =>
        [.. _integrations.OfType<IRepositoryIntegration>()];

    /// <summary>
    /// Finds an integration by its <see cref="IIntegration.Id"/>, e.g. a task's source.
    /// </summary>
    /// <returns>The integration, or <see langword="null"/> if there is none with that ID, e.g. it was removed.</returns>
    public IIntegration? Find(string id) => _integrations.FirstOrDefault(integration => integration.Id == id);

    /// <summary>
    /// Adds an integration.
    /// </summary>
    /// <exception cref="ArgumentException">An integration with the same ID is already added.</exception>
    public void Add(IIntegration integration)
    {
        ArgumentNullException.ThrowIfNull(integration);
        ArgumentException.ThrowIfNullOrWhiteSpace(integration.Id, nameof(integration));

        lock (_lock)
        {
            if (Find(integration.Id) is not null)
            {
                throw new ArgumentException(
                    $"An integration with ID '{integration.Id}' is already added.",
                    nameof(integration));
            }

            _integrations = [.. _integrations, integration];
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Removes an integration. Its credentials are left in the secret store: delete them with
    /// <see cref="IntegrationCredentials.DeleteAsync"/>.
    /// </summary>
    /// <returns>Whether an integration with that ID was added.</returns>
    public bool Remove(string id)
    {
        lock (_lock)
        {
            if (Find(id) is not { } integration)
            {
                return false;
            }

            _integrations = [.. _integrations.Where(other => !ReferenceEquals(other, integration))];
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }
}
