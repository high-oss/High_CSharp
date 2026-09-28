// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using System.Net;
using System.Text;

namespace High.OpenApi.Tests;

public sealed record ReceivedRequest(string Method, string Url, IReadOnlyDictionary<string, string> Headers, string Body);

/// <summary>
/// A real HTTP server on a random loopback port, backed by <see cref="HttpListener"/>.
/// Tests assert on actual exchanges rather than a mocked <see cref="HttpMessageHandler"/>,
/// so a change in how the SDK builds requests is caught rather than asserted around.
/// </summary>
public sealed class TestServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly List<ReceivedRequest> _requests = new();
    private readonly object _lock = new();
    private readonly Func<HttpListenerContext, int, Task> _handler;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loop;

    public string BaseUrl { get; }

    public IReadOnlyList<ReceivedRequest> Requests
    {
        get { lock (_lock) return _requests.ToList(); }
    }

    public TestServer(Func<HttpListenerContext, int, Task> handler)
    {
        _handler = handler;
        _listener = new HttpListener();
        var port = GetFreePort();
        BaseUrl = $"http://127.0.0.1:{port}";
        _listener.Prefixes.Add($"{BaseUrl}/");
        _listener.Start();
        _loop = Task.Run(AcceptLoop);
    }

    private async Task AcceptLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().WaitAsync(_cts.Token).ConfigureAwait(false);
            }
            catch (Exception) when (_cts.IsCancellationRequested)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            _ = Task.Run(() => HandleAsync(context));
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        string body;
        using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
        {
            body = await reader.ReadToEndAsync().ConfigureAwait(false);
        }

        var headers = context.Request.Headers.AllKeys
            .Where(k => k is not null)
            .ToDictionary(k => k!.ToLowerInvariant(), k => context.Request.Headers[k] ?? string.Empty);

        int index;
        lock (_lock)
        {
            index = _requests.Count;
            _requests.Add(new ReceivedRequest(context.Request.HttpMethod, context.Request.RawUrl ?? string.Empty, headers, body));
        }

        await _handler(context, index).ConfigureAwait(false);
    }

    private static int GetFreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Stop();
        _listener.Close();
        _cts.Dispose();
    }
}

/// <summary>Response-writing helpers for handlers.</summary>
public static class TestServerResponses
{
    public static async Task JsonAsync(HttpListenerContext context, int status, string json, IReadOnlyDictionary<string, string>? headers = null)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        if (headers is not null)
        {
            foreach (var (key, value) in headers) context.Response.Headers[key] = value;
        }
        var bytes = Encoding.UTF8.GetBytes(json);
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        context.Response.OutputStream.Close();
    }

    public static Task EnvelopeAsync(HttpListenerContext context, int status, string requestId, string dataJson, IReadOnlyDictionary<string, string>? headers = null) =>
        JsonAsync(context, status, $"{{\"requestId\":\"{requestId}\",\"data\":{dataJson}}}", headers);
}
