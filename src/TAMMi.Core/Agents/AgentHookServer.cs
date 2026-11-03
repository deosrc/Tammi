using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TAMMi.Core.Agents;

/// <summary>
/// The local channel that agents' hooks report their events on: a small HTTP server on the loopback address, on a
/// port chosen when the first agent registers.
/// </summary>
/// <remarks>
/// Each agent gets its own token (see <see cref="AgentHookEndpoint"/>). Hooks POST each event as JSON, with the token
/// as a bearer token. Requests with an unknown token are refused. The server answers each event with an empty 200
/// response, which tells the agent to carry on as it would without the hook.
/// </remarks>
public sealed class AgentHookServer : IAsyncDisposable
{
    /// <summary>
    /// The path that hooks POST their events to.
    /// </summary>
    public const string EventsPath = "/agent-hooks";

    /// <summary>
    /// The largest event accepted. Events hold tool inputs and outputs, e.g. a whole file the agent wrote.
    /// </summary>
    internal const int MaxEventBytes = 32 * 1024 * 1024;

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    private readonly ConcurrentDictionary<string, AgentHookRegistration> _registrations = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _stopping = new();
    private readonly Lock _lock = new();
    private TcpListener? _listener;
    private Task _accepting = Task.CompletedTask;
    private bool _disposed;

    /// <summary>
    /// Gives an agent an endpoint for its hooks, starting the server if it isn't running yet.
    /// </summary>
    /// <returns>The registration. Dispose it once the agent has ended.</returns>
    /// <exception cref="SocketException">The server couldn't be started.</exception>
    /// <exception cref="ObjectDisposedException">The server has been disposed.</exception>
    public AgentHookRegistration Register()
    {
        var port = EnsureStarted();
        var token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        var url = new Uri($"http://{IPAddress.Loopback}:{port}{EventsPath}");
        var registration = new AgentHookRegistration(new AgentHookEndpoint(url, token), this);
        _registrations[token] = registration;
        return registration;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        await _stopping.CancelAsync().ConfigureAwait(false);
        _listener?.Stop();
        await _accepting.ConfigureAwait(false);
        _registrations.Clear();
        _stopping.Dispose();
    }

    internal void Unregister(AgentHookRegistration registration) =>
        _registrations.TryRemove(KeyValuePair.Create(registration.Endpoint.Token, registration));

    private int EnsureStarted()
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_listener is null)
            {
                var listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                _listener = listener;
                _accepting = AcceptAsync(listener);
            }

            return ((IPEndPoint)_listener.LocalEndpoint).Port;
        }
    }

    private async Task AcceptAsync(TcpListener listener)
    {
        while (true)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(_stopping.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException
                || (ex is SocketException && _stopping.IsCancellationRequested))
            {
                return;
            }
            catch (SocketException)
            {
                // A connection that failed before it was accepted.
                continue;
            }

            _ = HandleAsync(client);
        }
    }

    private async Task HandleAsync(TcpClient client)
    {
        using var _ = client;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token);
        timeout.CancelAfter(RequestTimeout);
        try
        {
            var stream = client.GetStream();
            var status = await HandleRequestAsync(new HookRequestReader(stream), timeout.Token).ConfigureAwait(false);
            var response = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 {(int)status} {status}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(response, timeout.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException
            or ObjectDisposedException)
        {
            // The connection was lost, or the request took too long. The hook carries on without TAMMi.
        }
    }

    private async Task<HttpStatusCode> HandleRequestAsync(HookRequestReader reader, CancellationToken cancellationToken)
    {
        string method, target;
        IReadOnlyDictionary<string, string> headers;
        try
        {
            (method, target, headers) = await reader.ReadHeadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidDataException)
        {
            return HttpStatusCode.BadRequest;
        }

        var path = target.Split('?', 2)[0];
        if (path != EventsPath)
        {
            return HttpStatusCode.NotFound;
        }

        if (method != "POST")
        {
            return HttpStatusCode.MethodNotAllowed;
        }

        if (!headers.TryGetValue("Authorization", out var authorization)
            || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            || !_registrations.TryGetValue(authorization["Bearer ".Length..].Trim(), out var registration))
        {
            return HttpStatusCode.Unauthorized;
        }

        JsonDocument hookEvent;
        try
        {
            var body = await reader.ReadBodyAsync(headers, MaxEventBytes, cancellationToken).ConfigureAwait(false);
            if (body is null)
            {
                return HttpStatusCode.RequestEntityTooLarge;
            }

            hookEvent = JsonDocument.Parse(body);
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException)
        {
            return HttpStatusCode.BadRequest;
        }

        using (hookEvent)
        {
            registration.OnEventReceived(hookEvent.RootElement);
        }

        return HttpStatusCode.OK;
    }
}
