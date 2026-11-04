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

    /// <summary>
    /// The sessions it has started, in the order they were started.
    /// </summary>
    public List<FakePtySession> Sessions { get; } = [];

    public IPtySession Start(ProcessRequest request, PtySize size)
    {
        Requests.Add((request, size));
        var session = new FakePtySession();
        Sessions.Add(session);
        return session;
    }
}
