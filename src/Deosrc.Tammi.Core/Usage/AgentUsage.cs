namespace Deosrc.Tammi.Core.Usage;

/// <summary>
/// The usage of one AI agent at a point in time.
/// </summary>
/// <param name="Limits">The agent's limits, shortest period first.</param>
/// <param name="FetchedAt">When the usage was read.</param>
public sealed record AgentUsage(IReadOnlyList<AgentUsageLimit> Limits, DateTimeOffset FetchedAt);
