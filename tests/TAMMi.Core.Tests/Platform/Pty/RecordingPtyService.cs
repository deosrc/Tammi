using TAMMi.Core.Platform;
using TAMMi.Core.Tests.Agents;

namespace TAMMi.Core.Tests.Platform.Pty;

/// <summary>
/// A pseudoterminal service that records what it's asked to start, and starts nothing.
/// </summary>
internal sealed class RecordingPtyService : IPtyService
{
    public List<(ProcessRequest Request, PtySize Size)> Requests { get; } = [];

    public ProcessRequest DefaultShell { get; init; } = new("sh");

    public ProcessRequest GetDefaultShell() => DefaultShell;

    public IPtySession Start(ProcessRequest request, PtySize size)
    {
        Requests.Add((request, size));
        return new FakePtySession();
    }
}
