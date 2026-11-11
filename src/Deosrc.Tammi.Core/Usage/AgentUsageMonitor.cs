using Deosrc.Tammi.Core.Settings;

namespace Deosrc.Tammi.Core.Usage;

/// <summary>
/// Keeps the latest usage of the AI agents Tammi can read, and which agent the tab shows.
/// </summary>
public sealed class AgentUsageMonitor
{
    /// <summary>
    /// Usage newer than this isn't read again unless asked for.
    /// </summary>
    public static readonly TimeSpan MinimumRefreshInterval = TimeSpan.FromSeconds(30);

    private readonly IReadOnlyList<IAgentUsageProvider> _providers;
    private readonly ISettingsStore _settings;
    private readonly TimeProvider _time;
    private readonly Dictionary<string, AgentUsageState> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public AgentUsageMonitor(IEnumerable<IAgentUsageProvider> providers, ISettingsStore settings, TimeProvider? time = null)
    {
        _providers = [.. providers];
        _settings = settings;
        _time = time ?? TimeProvider.System;
        _settings.Changed += () => Changed?.Invoke();
    }

    /// <summary>
    /// Raised when usage is read or the tab's agent changes. It can be raised on any thread.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// The agents whose usage can be shown.
    /// </summary>
    public IReadOnlyList<string> AgentNames => [.. _providers.Select(provider => provider.AgentName)];

    /// <summary>
    /// The agent the tab shows: the one chosen in the settings, or else the first one.
    /// </summary>
    public string? TabAgentName
    {
        get
        {
            var chosen = _settings.Current.Agents.UsageAgentName;
            return _providers.Select(provider => provider.AgentName)
                .FirstOrDefault(name => string.Equals(name, chosen, StringComparison.OrdinalIgnoreCase))
                ?? _providers.FirstOrDefault()?.AgentName;
        }
    }

    /// <summary>
    /// Chooses the agent the tab shows.
    /// </summary>
    public Task SetTabAgentAsync(string agentName) =>
        _settings.UpdateAsync(settings => settings with { Agents = settings.Agents with { UsageAgentName = agentName } });

    /// <summary>
    /// The latest state for an agent, which is empty until it has been read.
    /// </summary>
    public AgentUsageState Get(string agentName)
    {
        lock (_lock)
        {
            return _states.GetValueOrDefault(agentName) ?? AgentUsageState.Empty;
        }
    }

    /// <summary>
    /// Reads an agent's usage, unless it was read recently and <paramref name="force"/> is <c>false</c>.
    /// </summary>
    public async Task RefreshAsync(string agentName, bool force = false, CancellationToken cancellationToken = default)
    {
        var provider = _providers.FirstOrDefault(p => string.Equals(p.AgentName, agentName, StringComparison.OrdinalIgnoreCase));
        if (provider is null)
        {
            return;
        }

        var previous = Get(provider.AgentName);
        if (!force && previous.LastAttempt is { } last && _time.GetUtcNow() - last < MinimumRefreshInterval)
        {
            return;
        }

        var now = _time.GetUtcNow();
        AgentUsageState next;
        try
        {
            next = new AgentUsageState(await provider.GetUsageAsync(cancellationToken), null, now);
        }
        catch (AgentUsageException ex)
        {
            // Keeps the last good reading, so the bars don't vanish on a failed refresh.
            next = new AgentUsageState(previous.Usage, ex.Message, now);
        }

        lock (_lock)
        {
            _states[provider.AgentName] = next;
        }

        Changed?.Invoke();
    }
}
