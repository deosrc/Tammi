namespace Deosrc.Tammi.Core.Git;

/// <summary>
/// Base class for errors raised by Tammi's git access.
/// </summary>
public class GitException : Exception
{
    public GitException(string message)
        : base(message)
    {
    }

    public GitException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
