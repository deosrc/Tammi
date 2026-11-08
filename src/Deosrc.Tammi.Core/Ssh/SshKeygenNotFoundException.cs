namespace Deosrc.Tammi.Core.Ssh;

/// <summary>
/// <c>ssh-keygen</c> can't be found, so keys can't be generated. It comes with OpenSSH and with Git for Windows.
/// </summary>
public sealed class SshKeygenNotFoundException()
    : SshKeyException("Couldn't find ssh-keygen. Install OpenSSH, or Git for Windows on Windows, to generate keys.");
