// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using System.Net;
using System.Net.WebSockets;
using System.Text;

namespace High.OpenApi.Tests;

/// <summary>
/// A real local WebSocket server on a random loopback port, backed by
/// <see cref="HttpListener"/>'s own <c>AcceptWebSocketAsync</c> — never a
/// mocked socket. Each connection is handed to <paramref name="onConnected"/>
/// (set via the constructor) on its own task, so a test can script exactly
/// what the server sends and when.
/// </summary>
public sealed class FeedTestServer : IAsyncDisposable
{
    private readonly HttpListener _listener;
    private readonly Func<WebSocket, CancellationToken, Task> _onConnected;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _acceptLoop;

    public string WsUrl { get; }

    public FeedTestServer(Func<WebSocket, CancellationToken, Task> onConnected)
    {
        _onConnected = onConnected;
        _listener = new HttpListener();
        var port = GetFreePort();
        var httpUrl = $"http://127.0.0.1:{port}/";
        WsUrl = $"ws://127.0.0.1:{port}/";
        _listener.Prefixes.Add(httpUrl);
        _listener.Start();
        _acceptLoop = Task.Run(AcceptLoopAsync);
    }

    private async Task AcceptLoopAsync()
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

            _ = Task.Run(() => HandleConnectionAsync(context));
        }
    }

    private async Task HandleConnectionAsync(HttpListenerContext context)
    {
        if (!context.Request.IsWebSocketRequest)
        {
            context.Response.StatusCode = 400;
            context.Response.Close();
            return;
        }

        HttpListenerWebSocketContext wsContext;
        try
        {
            wsContext = await context.AcceptWebSocketAsync(null).ConfigureAwait(false);
        }
        catch
        {
            return;
        }

        try
        {
            await _onConnected(wsContext.WebSocket, _cts.Token).ConfigureAwait(false);
        }
        catch (Exception) when (_cts.IsCancellationRequested)
        {
            // shutting down
        }
    }

    private static int GetFreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();

        // HttpListener.Stop() can block waiting for an open WebSocket-upgraded
        // connection to finish, which a test that leaves its handler running
        // (or whose client already disconnected) may never do promptly. Abort()
        // is documented to tear down immediately without waiting for pending
        // requests/connections, which is what a test teardown needs — this
        // suite creates and disposes many of these per run, and one blocked
        // Stop() call would stall every test after it.
        _listener.Abort();

        try
        {
            await _acceptLoop.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        }
        catch
        {
            // best effort
        }
        _cts.Dispose();
    }
}

/// <summary>Small helpers for scripting a server-side socket in a test.</summary>
public static class FeedTestServerFrames
{
    public static Task SendTextAsync(WebSocket socket, string json, CancellationToken ct) =>
        socket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, ct);

    /// <summary>Sends <paramref name="json"/> split across several frames (none of them <c>EndOfMessage</c> until the last), exercising partial-frame reassembly.</summary>
    public static async Task SendTextInChunksAsync(WebSocket socket, string json, int chunkSize, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        for (var i = 0; i < bytes.Length; i += chunkSize)
        {
            var length = Math.Min(chunkSize, bytes.Length - i);
            var end = i + length >= bytes.Length;
            await socket.SendAsync(new ArraySegment<byte>(bytes, i, length), WebSocketMessageType.Text, end, ct).ConfigureAwait(false);
        }
    }

    public static async Task<string> ReceiveTextAsync(WebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[16384];
        using var stream = new MemoryStream();
        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, ct).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close) return string.Empty;
            stream.Write(buffer, 0, result.Count);
            if (result.EndOfMessage) break;
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public const string OkAck = "{\"stat\":\"Ok\",\"type\":\"cn\",\"msg\":\"successful\",\"stCode\":200,\"maxScripPerConn\":500,\"maxScripPerReq\":200,\"sType\":\"v2.0\"}";

    public static string OkAckWithLimits(int maxScripPerConn, int maxScripPerReq) =>
        $"{{\"stat\":\"Ok\",\"type\":\"cn\",\"msg\":\"successful\",\"stCode\":200,\"maxScripPerConn\":{maxScripPerConn},\"maxScripPerReq\":{maxScripPerReq},\"sType\":\"v2.0\"}}";

    public static string NotOkAck(int stCode, string msg) =>
        $"{{\"stat\":\"NotOk\",\"type\":\"cn\",\"msg\":\"{msg}\",\"stCode\":{stCode}}}";

    public const string SubOkAck = "[{\"stat\":\"Ok\",\"type\":\"sub\",\"msg\":\"successful\",\"stCode\":200}]";
    public const string UnsubOkAck = "[{\"stat\":\"Ok\",\"type\":\"unsub\",\"msg\":\"successful\",\"stCode\":200}]";
}
