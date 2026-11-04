using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Tammi.Core.Agents;

namespace Tammi.Core.Tests.Agents;

public sealed class AgentHookServerTests : IAsyncDisposable
{
    private readonly AgentHookServer _server = new();
    private readonly HttpClient _client = new();

    [Fact]
    public void Register_GivesEachAgentItsOwnTokenOnLoopback()
    {
        using var first = _server.Register();
        using var second = _server.Register();

        Assert.Equal(IPAddress.Loopback.ToString(), first.Endpoint.Url.Host);
        Assert.Equal(AgentHookServer.EventsPath, first.Endpoint.Url.AbsolutePath);
        Assert.Equal(first.Endpoint.Url, second.Endpoint.Url);
        Assert.NotEqual(first.Endpoint.Token, second.Endpoint.Token);
        Assert.Equal(64, first.Endpoint.Token.Length);
        Assert.Equal(
            first.Endpoint.Token,
            first.Endpoint.Environment[AgentHookEndpoint.TokenVariable]);
    }

    [Fact]
    public async Task Post_WithToken_RaisesEventForThatAgent()
    {
        using var registration = _server.Register();
        using var other = _server.Register();
        var received = Receive(registration);
        other.EventReceived += _ => Assert.Fail("The event went to the wrong agent.");

        using var response = await PostAsync(registration.Endpoint, """{"hook_event_name":"Stop"}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal("Stop", await received);
    }

    [Fact]
    public async Task Post_RaisesEventBeforeResponding()
    {
        using var registration = _server.Register();
        string? name = null;
        registration.EventReceived += hookEvent => name = hookEvent.GetProperty("hook_event_name").GetString();

        using var response = await PostAsync(registration.Endpoint, """{"hook_event_name":"Stop"}""");

        Assert.Equal("Stop", name);
    }

    [Fact]
    public async Task Post_WithChunkedBody_RaisesEvent()
    {
        using var registration = _server.Register();
        var received = Receive(registration);
        var json = """{"hook_event_name":"PostToolUse","tool_response":"__"}""".Replace("__", new string('x', 100_000));
        using var content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(json)));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using var response = await PostAsync(registration.Endpoint, content, chunked: true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("PostToolUse", await received);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Bearer wrong")]
    [InlineData("Basic dXNlcjpwYXNz")]
    public async Task Post_WithoutKnownToken_IsRefused(string? authorization)
    {
        using var registration = _server.Register();
        registration.EventReceived += _ => Assert.Fail("An event without the token was accepted.");
        using var request = new HttpRequestMessage(HttpMethod.Post, registration.Endpoint.Url)
        {
            Content = new StringContent("""{"hook_event_name":"Stop"}"""),
        };
        if (authorization is not null)
        {
            request.Headers.TryAddWithoutValidation("Authorization", authorization);
        }

        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_AfterRegistrationIsDisposed_IsRefused()
    {
        var registration = _server.Register();
        registration.Dispose();

        using var response = await PostAsync(registration.Endpoint, """{"hook_event_name":"Stop"}""");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithInvalidJson_IsBadRequest()
    {
        using var registration = _server.Register();

        using var response = await PostAsync(registration.Endpoint, "not json");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_IsNotAllowed()
    {
        using var registration = _server.Register();

        using var response = await _client.GetAsync(registration.Endpoint.Url, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task Post_ToOtherPath_IsNotFound()
    {
        using var registration = _server.Register();
        var endpoint = registration.Endpoint with { Url = new Uri(registration.Endpoint.Url, "/other") };

        using var response = await PostAsync(endpoint, """{"hook_event_name":"Stop"}""");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithTooLongContentLength_IsRefused()
    {
        using var registration = _server.Register();
        using var socket = new TcpClient();
        await socket.ConnectAsync(IPAddress.Loopback, registration.Endpoint.Url.Port, TestContext.Current.CancellationToken);
        var stream = socket.GetStream();
        var request = $"POST {AgentHookServer.EventsPath} HTTP/1.1\r\nHost: localhost\r\n"
            + $"Authorization: Bearer {registration.Endpoint.Token}\r\n"
            + $"Content-Length: {AgentHookServer.MaxEventBytes + 1}\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(request), TestContext.Current.CancellationToken);

        using var reader = new StreamReader(stream);
        var statusLine = await reader.ReadLineAsync(TestContext.Current.CancellationToken);

        Assert.StartsWith("HTTP/1.1 413 ", statusLine);
    }

    [Fact]
    public async Task DisposeAsync_StopsListening()
    {
        var registration = _server.Register();

        await _server.DisposeAsync();

        await Assert.ThrowsAnyAsync<HttpRequestException>(
            () => PostAsync(registration.Endpoint, """{"hook_event_name":"Stop"}"""));
        Assert.Throws<ObjectDisposedException>(_server.Register);
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _server.DisposeAsync();
    }

    private static Task<string?> Receive(AgentHookRegistration registration)
    {
        var received = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        registration.EventReceived += hookEvent =>
            received.TrySetResult(hookEvent.GetProperty("hook_event_name").GetString());
        return received.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    private Task<HttpResponseMessage> PostAsync(AgentHookEndpoint endpoint, string json) =>
        PostAsync(endpoint, new StringContent(json, Encoding.UTF8, "application/json"), chunked: false);

    private async Task<HttpResponseMessage> PostAsync(AgentHookEndpoint endpoint, HttpContent content, bool chunked)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint.Url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", endpoint.Token);
        request.Headers.TransferEncodingChunked = chunked;
        return await _client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
