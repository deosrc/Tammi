namespace Deosrc.Tammi.Core.Usage;

/// <summary>
/// The usage of an agent couldn't be read. The message says why, and is safe to show to the user.
/// </summary>
public sealed class AgentUsageException : Exception
{
    public AgentUsageException(string message) : base(message)
    {
    }

    public AgentUsageException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
