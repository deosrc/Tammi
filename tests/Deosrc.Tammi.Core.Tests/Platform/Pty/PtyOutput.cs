using System.Text;
using Tammi.Core.Platform;

namespace Tammi.Core.Tests.Platform.Pty;

/// <summary>
/// Reads a pseudoterminal session's output in tests.
/// </summary>
internal sealed class PtyOutput(IPtySession session)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private readonly List<byte> _bytes = [];
    private bool _ended;

    /// <summary>
    /// The output read so far, decoded as UTF-8.
    /// </summary>
    public string Text => Encoding.UTF8.GetString([.. _bytes]);

    /// <summary>
    /// Reads until the output ends.
    /// </summary>
    public async Task<string> ReadToEndAsync()
    {
        using var timeout = CreateTimeout();
        while (await ReadOnceAsync(timeout.Token))
        {
        }

        return Text;
    }

    /// <summary>
    /// Reads until the output contains some text.
    /// </summary>
    public async Task<string> ReadUntilAsync(string text)
    {
        using var timeout = CreateTimeout();
        while (!Text.Contains(text, StringComparison.Ordinal))
        {
            if (!await ReadOnceAsync(timeout.Token))
            {
                Assert.Fail($"The output ended without '{text}'. It was:\n{Text}");
            }
        }

        return Text;
    }

    private static CancellationTokenSource CreateTimeout()
    {
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(Timeout);
        return timeout;
    }

    private async Task<bool> ReadOnceAsync(CancellationToken cancellationToken)
    {
        if (_ended)
        {
            return false;
        }

        var buffer = new byte[4096];
        var count = await session.ReadAsync(buffer, cancellationToken);
        _bytes.AddRange(buffer.AsSpan(0, count));
        _ended = count == 0;
        return !_ended;
    }
}
