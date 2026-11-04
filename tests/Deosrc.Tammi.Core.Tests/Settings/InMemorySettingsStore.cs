using Tammi.Core.Settings;

namespace Tammi.Core.Tests.Settings;

/// <summary>
/// A settings store that keeps the settings in memory.
/// </summary>
internal sealed class InMemorySettingsStore(AppSettings? settings = null) : ISettingsStore
{
    public AppSettings Current { get; private set; } = (settings ?? new AppSettings()).Normalize();

    public string? LoadProblem => null;

    public event Action? Changed;

    public Task<AppSettings> UpdateAsync(Func<AppSettings, AppSettings> update, CancellationToken cancellationToken = default)
    {
        Current = update(Current).Normalize();
        Changed?.Invoke();
        return Task.FromResult(Current);
    }
}
