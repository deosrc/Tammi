using Tammi.Core.Platform;

namespace Tammi.Core.Tests.Platform.Pty;

/// <summary>
/// A pseudoterminal service that can't start anything: each start throws the given exception.
/// </summary>
internal sealed class FailingPtyService(Exception exception) : IPtyService
{
    public ProcessRequest GetDefaultShell() => new("sh");

    public IPtySession Start(ProcessRequest request, PtySize size) => throw exception;
}
