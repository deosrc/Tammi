using System.Text;
using Tammi.Core.Terminals;

namespace Tammi.Core.Tests.Terminals;

public class TerminalSessionTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly ScriptedPtySession _pty = new();

    [Fact]
    public async Task Reader_GetsOutputAsItArrives()
    {
        var session = new TerminalSession(_pty);
        var reader = session.OpenReader();

        _pty.Output("hello ");
        Assert.Equal("hello ", await ReadAsync(reader, 6));
        _pty.Output("world");
        Assert.Equal("world", await ReadAsync(reader, 5));
    }

    [Fact]
    public async Task Reader_OpenedLater_ReplaysTheOutputSoFar()
    {
        var session = new TerminalSession(_pty);
        _pty.Output("first ");
        _pty.Output("second");
        await WaitForOutputAsync(session, "second");

        Assert.Equal("first second", await ReadAsync(session.OpenReader(), 12));
    }

    [Fact]
    public async Task Readers_EachHaveTheirOwnPlace()
    {
        var session = new TerminalSession(_pty);
        var first = session.OpenReader();
        _pty.Output("abc");
        Assert.Equal("abc", await ReadAsync(first, 3));

        var second = session.OpenReader();
        _pty.Output("def");

        Assert.Equal("abcdef", await ReadAsync(second, 6));
        Assert.Equal("def", await ReadAsync(first, 3));
    }

    [Fact]
    public async Task Reader_SmallBuffer_ReadsInPieces()
    {
        var session = new TerminalSession(_pty);
        var reader = session.OpenReader();
        _pty.Output("abcdef");

        var buffer = new byte[4];
        var count = await reader.ReadAsync(buffer, TestContext.Current.CancellationToken).AsTask().WaitAsync(Timeout, TestContext.Current.CancellationToken);
        Assert.Equal("abcd", Encoding.UTF8.GetString(buffer, 0, count));
        Assert.Equal("ef", await ReadAsync(reader, 2));
    }

    [Fact]
    public async Task History_OverLimit_DropsTheOldestOutput()
    {
        var session = new TerminalSession(_pty, historyLimit: 6);
        _pty.Output("old");
        _pty.Output("mid");
        _pty.Output("new");
        await WaitForOutputAsync(session, "new");

        Assert.Equal("midnew", await ReadAsync(session.OpenReader(), 6));
    }

    [Fact]
    public async Task History_ChunkOverLimit_IsKeptWhole()
    {
        var session = new TerminalSession(_pty, historyLimit: 2);
        _pty.Output("old");
        _pty.Output("newest");
        await WaitForOutputAsync(session, "newest");

        Assert.Equal("newest", await ReadAsync(session.OpenReader(), 6));
    }

    [Fact]
    public async Task Reader_FallenBehind_SkipsToTheOldestOutputKept()
    {
        var session = new TerminalSession(_pty, historyLimit: 3);
        var reader = session.OpenReader();
        _pty.Output("one");
        _pty.Output("two");
        await WaitForOutputAsync(session, "two");

        Assert.Equal("two", await ReadAsync(reader, 3));
    }

    [Fact]
    public async Task Reader_AfterExit_ReadsTheRestThenEnds()
    {
        var session = new TerminalSession(_pty);
        _pty.Output("bye");
        _pty.Exit(0);
        await session.OutputEnded.WaitAsync(Timeout, TestContext.Current.CancellationToken);

        var reader = session.OpenReader();
        Assert.Equal("bye", await ReadAsync(reader, 3));
        Assert.Equal(0, await reader.ReadAsync(new byte[16], TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task OutputError_EndsTheOutput()
    {
        var session = new TerminalSession(_pty);
        var reader = session.OpenReader();

        _pty.Fail();

        await session.OutputEnded.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        Assert.Equal(0, await reader.ReadAsync(new byte[16], TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Read_Cancelled_Throws()
    {
        var session = new TerminalSession(_pty);
        using var cancellation = new CancellationTokenSource();
        var read = session.OpenReader().ReadAsync(new byte[16], cancellation.Token);

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read.AsTask().WaitAsync(Timeout, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Reads until <paramref name="length"/> bytes have been read.
    /// </summary>
    private static async Task<string> ReadAsync(TerminalOutputReader reader, int length)
    {
        var buffer = new byte[length];
        var total = 0;
        while (total < length)
        {
            var count = await reader.ReadAsync(buffer.AsMemory(total), TestContext.Current.CancellationToken)
                .AsTask()
                .WaitAsync(Timeout, TestContext.Current.CancellationToken);
            Assert.NotEqual(0, count);
            total += count;
        }

        return Encoding.UTF8.GetString(buffer);
    }

    /// <summary>
    /// Waits until the output the session keeps ends with <paramref name="end"/>.
    /// </summary>
    private static async Task WaitForOutputAsync(TerminalSession session, string end)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (DateTime.UtcNow < deadline)
        {
            // Read everything kept so far: the read that waits for more is cancelled.
            var reader = session.OpenReader();
            var buffer = new byte[1024];
            var total = 0;
            using var poll = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
            try
            {
                while (true)
                {
                    total += await reader.ReadAsync(buffer.AsMemory(total), poll.Token);
                }
            }
            catch (OperationCanceledException)
            {
            }

            if (Encoding.UTF8.GetString(buffer, 0, total).EndsWith(end, StringComparison.Ordinal))
            {
                return;
            }
        }

        Assert.Fail($"The output didn't end with '{end}'.");
    }
}
