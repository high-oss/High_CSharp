// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using System.Net.WebSockets;
using System.Text;

namespace High.OpenApi.Feed;

/// <summary>
/// The raw <see cref="ClientWebSocket"/> plumbing: one connection, JSON text
/// frames in both directions.
///
/// Two things this exists specifically to get right:
/// <list type="bullet">
/// <item><see cref="ReceiveMessageAsync"/> accumulates <see cref="ClientWebSocket.ReceiveAsync(ArraySegment{byte}, CancellationToken)"/>
/// results until <c>EndOfMessage</c> is true. A single call can return a
/// partial frame for a large payload; treating that partial buffer as the
/// whole message would silently truncate it.</item>
/// <item><see cref="SendAsync"/> serialises sends behind a semaphore — only one
/// send may be in flight on a <see cref="ClientWebSocket"/> at a time, or the
/// socket faults.</item>
/// </list>
/// </summary>
internal sealed class FeedSocket : IAsyncDisposable
{
    private readonly ClientWebSocket _socket = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public WebSocketState State => _socket.State;

    public Task ConnectAsync(Uri uri, CancellationToken cancellationToken) =>
        _socket.ConnectAsync(uri, cancellationToken);

    public async Task SendAsync(string json, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>
    /// One complete text message, or <c>null</c> when the socket closed
    /// (gracefully or not) — the caller treats either as a transport failure
    /// for reconnect purposes, since a deliberate close goes through
    /// <see cref="DisposeAsync"/> instead of this loop.
    /// </summary>
    public async Task<string?> ReceiveMessageAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        using var stream = new MemoryStream();
        while (true)
        {
            WebSocketReceiveResult result;
            try
            {
                result = await _socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
            }
            catch (WebSocketException)
            {
                return null;
            }
            catch (ObjectDisposedException)
            {
                return null;
            }

            if (result.MessageType == WebSocketMessageType.Close) return null;

            stream.Write(buffer, 0, result.Count);
            if (result.EndOfMessage) break;
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                // CloseOutputAsync sends the close frame without waiting for the peer to send
                // one back. CloseAsync does wait, and with an uncancellable token it waits
                // forever against a server that has stopped responding — which is exactly the
                // state we are in after an auth rejection, or any unclean disconnect. Closing
                // a feed must never hang the caller, so send and move on, with a deadline in
                // case even the send stalls.
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "closing", deadline.Token).ConfigureAwait(false);
            }
        }
        catch
        {
            // Best-effort close — the socket is going away either way.
        }
        finally
        {
            _socket.Dispose();
            _sendLock.Dispose();
        }
    }
}
