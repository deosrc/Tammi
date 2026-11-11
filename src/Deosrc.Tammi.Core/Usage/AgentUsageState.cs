namespace Deosrc.Tammi.Core.Usage;

/// <summary>
/// What's known about an agent's usage.
/// </summary>
/// <param name="Usage">The last usage read, or <c>null</c> if none has been.</param>
/// <param name="Error">Why the last attempt failed, or <c>null</c> if it didn't.</param>
/// <param name="LastAttempt">When usage was last read, or <c>null</c> if it hasn't been.</param>
public sealed record AgentUsageState(AgentUsage? Usage, string? Error, DateTimeOffset? LastAttempt)
{
    public static AgentUsageState Empty { get; } = new(null, null, null);
}
