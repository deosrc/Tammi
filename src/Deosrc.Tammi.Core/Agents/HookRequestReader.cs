using System.Buffers;
using System.Globalization;
using System.Text;

namespace Deosrc.Tammi.Core.Agents;

/// <summary>
/// Reads one HTTP/1.1 request from a connection to the <see cref="AgentHookServer"/>: its request line and headers,
/// then its body, sent with a <c>Content-Length</c> or in chunks.
/// </summary>
/// <remarks>
/// Only what hooks send is supported. Every method throws <see cref="InvalidDataException"/> for a request it can't
/// read.
/// </remarks>
internal sealed class HookRequestReader(Stream stream)
{
    /// <summary>
    /// The most that the request line and headers can take up.
    /// </summary>
    public const int MaxHeadBytes = 32 * 1024;

    private readonly byte[] _buffer = new byte[8 * 1024];
    private int _start;
    private int _end;

    /// <summary>
    /// Reads the request line and headers.
    /// </summary>
    /// <returns>The method, the request target and the headers, whose names are matched ignoring case.</returns>
    public async Task<(string Method, string Target, IReadOnlyDictionary<string, string> Headers)> ReadHeadAsync(
        CancellationToken cancellationToken)
    {
        var budget = MaxHeadBytes;
        var requestLine = await ReadLineAsync(budget, cancellationToken).ConfigureAwait(false);
        budget -= requestLine.Length;
        var parts = requestLine.Split(' ');
        if (parts.Length != 3 || !parts[2].StartsWith("HTTP/1.", StringComparison.Ordinal))
        {
            throw new InvalidDataException("The request line isn't HTTP/1.x.");
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (true)
        {
            var line = await ReadLineAsync(budget, cancellationToken).ConfigureAwait(false);
            budget -= line.Length + 2;
            if (line.Length == 0)
            {
                return (parts[0], parts[1], headers);
            }

            var colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon <= 0)
            {
                throw new InvalidDataException("A header has no name.");
            }

            var name = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();
            headers[name] = headers.TryGetValue(name, out var existing) ? $"{existing}, {value}" : value;
        }
    }

    /// <summary>
    /// Reads the body that <paramref name="headers"/> describe.
    /// </summary>
    /// <returns>The body, or <c>null</c> if it's longer than <paramref name="maxBytes"/>.</returns>
    public async Task<byte[]?> ReadBodyAsync(
        IReadOnlyDictionary<string, string> headers,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        if (headers.TryGetValue("Transfer-Encoding", out var encoding))
        {
            if (!encoding.Equals("chunked", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"The transfer encoding '{encoding}' isn't supported.");
            }

            return await ReadChunkedBodyAsync(maxBytes, cancellationToken).ConfigureAwait(false);
        }

        if (!headers.TryGetValue("Content-Length", out var lengthText))
        {
            return [];
        }

        if (!long.TryParse(lengthText, NumberStyles.None, CultureInfo.InvariantCulture, out var length))
        {
            throw new InvalidDataException("The Content-Length isn't a number.");
        }

        if (length > maxBytes)
        {
            return null;
        }

        var body = new byte[length];
        await ReadExactlyAsync(body, cancellationToken).ConfigureAwait(false);
        return body;
    }

    private async Task<byte[]?> ReadChunkedBodyAsync(int maxBytes, CancellationToken cancellationToken)
    {
        var body = new ArrayBufferWriter<byte>();
        while (true)
        {
            var sizeLine = await ReadLineAsync(MaxHeadBytes, cancellationToken).ConfigureAwait(false);
            var sizeText = sizeLine.Split(';')[0].Trim();
            if (!int.TryParse(sizeText, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var size)
                || size < 0)
            {
                throw new InvalidDataException("A chunk's size isn't a number.");
            }

            if (size == 0)
            {
                // Skip any trailers, up to the blank line that ends the request.
                while ((await ReadLineAsync(MaxHeadBytes, cancellationToken).ConfigureAwait(false)).Length > 0)
                {
                }

                return body.WrittenSpan.ToArray();
            }

            if (size > maxBytes - body.WrittenCount)
            {
                return null;
            }

            await ReadExactlyAsync(body.GetMemory(size)[..size], cancellationToken).ConfigureAwait(false);
            body.Advance(size);
            if ((await ReadLineAsync(2, cancellationToken).ConfigureAwait(false)).Length > 0)
            {
                throw new InvalidDataException("A chunk is longer than its size.");
            }
        }
    }

    // Reads up to a line feed, and drops it and a carriage return before it.
    private async Task<string> ReadLineAsync(int maxLength, CancellationToken cancellationToken)
    {
        var line = new ArrayBufferWriter<byte>();
        while (true)
        {
            var available = _buffer.AsSpan(_start, _end - _start);
            var newline = available.IndexOf((byte)'\n');
            var take = newline >= 0 ? newline : available.Length;
            if (line.WrittenCount + take > maxLength)
            {
                throw new InvalidDataException("A line in the request is too long.");
            }

            line.Write(available[..take]);
            if (newline >= 0)
            {
                _start += newline + 1;
                var bytes = line.WrittenSpan;
                if (bytes.Length > 0 && bytes[^1] == '\r')
                {
                    bytes = bytes[..^1];
                }

                return Encoding.Latin1.GetString(bytes);
            }

            _start = _end;
            await FillAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ReadExactlyAsync(Memory<byte> destination, CancellationToken cancellationToken)
    {
        var buffered = Math.Min(destination.Length, _end - _start);
        _buffer.AsMemory(_start, buffered).CopyTo(destination);
        _start += buffered;
        await stream.ReadExactlyAsync(destination[buffered..], cancellationToken).ConfigureAwait(false);
    }

    private async Task FillAsync(CancellationToken cancellationToken)
    {
        _start = 0;
        _end = await stream.ReadAsync(_buffer, cancellationToken).ConfigureAwait(false);
        if (_end == 0)
        {
            throw new InvalidDataException("The connection closed before the request ended.");
        }
    }
}
