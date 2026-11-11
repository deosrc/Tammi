namespace Deosrc.Tammi.Core.Usage;

/// <summary>
/// One usage limit of an AI agent's plan, e.g. the 5 hour or weekly limit.
/// </summary>
/// <param name="Name">Short name, e.g. "5h" or "Weekly".</param>
/// <param name="Description">What the limit covers, e.g. "5 hour session".</param>
/// <param name="PercentUsed">How much of the limit is used, from 0 to 100.</param>
/// <param name="ResetsAt">When the limit resets, or <c>null</c> if unknown.</param>
public sealed record AgentUsageLimit(string Name, string Description, double PercentUsed, DateTimeOffset? ResetsAt);
