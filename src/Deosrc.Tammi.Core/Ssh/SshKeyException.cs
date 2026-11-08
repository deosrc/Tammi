namespace Deosrc.Tammi.Core.Ssh;

/// <summary>
/// An SSH key couldn't be generated, read or removed.
/// </summary>
public class SshKeyException : Exception
{
    public SshKeyException(string message)
        : base(message)
    {
    }

    public SshKeyException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
