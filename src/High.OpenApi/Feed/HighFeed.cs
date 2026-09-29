// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;

namespace High.OpenApi.Feed;

/// <summary>
/// The HIGH live datafeed client — a separate client beside <see cref="HighClient"/>,
/// built from the same <see cref="HighClientOptions"/> and credentials, over a
/// raw <see cref="System.Net.WebSockets.ClientWebSocket"/>. No new dependency.
///
/// Production only: the host is <c>openapi-feed.high.live</c>. There is no
/// sandbox feed, so the constructor throws outright when the resolved
/// environment is <see cref="Environment.Sandbox"/> rather than quietly
/// connecting somewhere unexpected.
///
/// <code>
/// await using var feed = new HighFeed(new HighClientOptions { AccessToken = accessToken });
/// await feed.ConnectAsync();
/// await feed.SubscribeQuotesAsync(new[] { "NSE@2885" });
///
/// await foreach (var evt in feed.Events())
/// {
///     if (evt is QuoteEvent q) Console.WriteLine($"{q.ScripKey} {q.Quote.LastTradedPrice}");
/// }
/// </code>
///
/// Subscription is by HIGH scrip key only — the feed's own identifiers never
/// appear in the public surface. Ticks are deltas; this client owns the merge
/// and hands you a complete typed snapshot plus the fields that just changed,
/// on every tick, through both <see cref="Events"/> and the <c>On*</c> events.
///
/// Thread-safe: <c>ConnectAsync</c>, the <c>Subscribe*</c>/<c>Unsubscribe*</c>/
/// <c>Snapshot*</c> methods and reconnection may all be in flight from
/// different callers at once.
/// </summary>
public sealed class HighFeed : IAsyncDisposable
{
    private readonly ResolvedConfig _config;
    private readonly FeedStateMachine _stateMachine = new();
    private readonly Channel<FeedEvent> _events = Channel.CreateUnbounded<FeedEvent>();

    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private readonly SemaphoreSlim _requestLock = new(1, 1);
    private readonly object _registryLock = new();

    private readonly Dictionary<FeedKind, HashSet<string>> _subscribedByKind = new();
    private readonly Dictionary<string, string> _feedIdToScripKey = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TaskCompletionSource<bool>> _pendingSnapshots = new(StringComparer.Ordinal);

    private FeedSocket? _socket;
    private FeedSocket? _failingSocket;
    private CancellationTokenSource? _receiveLoopCts;
    private TaskCompletionSource<JsonArray>? _pendingControlAck;
    private int _maxScripPerConn = int.MaxValue;
    private int _maxScripPerReq = int.MaxValue;
    private long _lastActivityTicks;
    private volatile bool _closed;

    /// <summary>
    /// How long the client waits without receiving any frame before treating
    /// the connection as dead and reconnecting — the "dead-but-open socket"
    /// case the transport never errors on its own, so nothing else in this
    /// class would ever notice it. Internal so tests can shorten it to
    /// something they can wait out; the production default is generous
    /// because a quiet instrument can legitimately go tens of seconds between
    /// ticks without anything being wrong.
    /// </summary>
    internal TimeSpan IdleTimeout { get; set; } = TimeSpan.FromSeconds(45);

    /// <summary>Raised for every merged quote (market-watch) tick.</summary>
    public event EventHandler<QuoteEvent>? OnQuote;

    /// <summary>Raised for every merged book — both the one-level top-of-book split out of a market-watch tick and the five-level dedicated depth feed. See <see cref="FeedDepth.Source"/>.</summary>
    public event EventHandler<DepthEvent>? OnDepth;

    /// <summary>Raised for every merged index tick.</summary>
    public event EventHandler<IndexEvent>? OnIndex;

    /// <summary>Raised after a transport failure has been repaired: reconnected, re-authenticated, and every prior subscription reinstated.</summary>
    public event EventHandler? OnReconnected;

    /// <summary>Raised for a transport failure being handled (before a reconnect attempt) and for an auth failure that stops reconnection for good.</summary>
    public event EventHandler<Exception>? OnError;

    public HighFeed(HighClientOptions? options = null)
    {
        _config = ConfigResolver.Resolve(options ?? new HighClientOptions());

        if (_config.Environment == High.OpenApi.Environment.Sandbox)
        {
            throw new HighFeedError(
                "HighFeed is production-only — there is no sandbox datafeed. Build HighClientOptions with " +
                "Environment.Production (the default) or leave Environment unset. Pass WsBaseUrl only to point " +
                "a Production-environment client at a test endpoint.");
        }

        _stateMachine.QuoteReceived += evt => Emit(evt, OnQuote);
        _stateMachine.DepthReceived += evt => Emit(evt, OnDepth);
        _stateMachine.IndexReceived += evt => Emit(evt, OnIndex);
    }

    private void Emit<TEvent>(TEvent evt, EventHandler<TEvent>? handler) where TEvent : FeedEvent
    {
        handler?.Invoke(this, evt);
        _events.Writer.TryWrite(evt);
        SignalPendingSnapshot(evt.ScripKey);
    }

    /// <summary>
    /// The unified delivery form: every <see cref="QuoteEvent"/>, <see cref="DepthEvent"/>
    /// and <see cref="IndexEvent"/> in arrival order. Equivalent to the three
    /// <c>On*</c> events; use whichever idiom fits your caller.
    /// </summary>
    public async IAsyncEnumerable<FeedEvent> Events([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var evt in _events.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return evt;
        }
    }

    /// <summary>
    /// Opens the socket and authenticates. Sends the auth frame first and
    /// waits for its acknowledgement before this returns — no subscription can
    /// be queued ahead of it. Idempotent: a second call while already
    /// connected is a no-op.
    /// </summary>
    /// <exception cref="HighFeedAuthError">The server rejected the auth frame (<c>"stat":"NotOk"</c>). Never retried; this <see cref="HighFeed"/> will not reconnect on its own after this.</exception>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfClosed();
        await _connectLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_socket is not null) return;
            var socket = await ConnectAndAuthenticateAsync(cancellationToken).ConfigureAwait(false);
            _socket = socket;
            StartReceiveLoop(socket);
        }
        finally
        {
            _connectLock.Release();
        }
    }

    /// <summary>Closes the socket and stops delivery. Safe to call more than once.</summary>
    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        _closed = true;
        _receiveLoopCts?.Cancel();

        await _connectLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_socket is not null)
            {
                await _socket.DisposeAsync().ConfigureAwait(false);
                _socket = null;
            }
        }
        finally
        {
            _connectLock.Release();
        }

        _events.Writer.TryComplete();
    }

    public async ValueTask DisposeAsync() => await CloseAsync().ConfigureAwait(false);

    // ---- Subscribe / Unsubscribe --------------------------------------------

    /// <summary>Subscribes to market-watch (quote) ticks for the given HIGH scrip keys.</summary>
    /// <exception cref="HighFeedError">A key is not a recognised non-index scrip key (including any key the feed treats as an index — use <see cref="SubscribeIndicesAsync"/> instead), or the subscription would exceed <c>maxScripPerConn</c>.</exception>
    public Task SubscribeQuotesAsync(IReadOnlyCollection<string> scripKeys, CancellationToken cancellationToken = default) =>
        SubscribeInternalAsync(FeedKind.Quote, scripKeys, ScripKeyTranslator.TranslateQuoteOrDepth, cancellationToken);

    public Task UnsubscribeQuotesAsync(IReadOnlyCollection<string> scripKeys, CancellationToken cancellationToken = default) =>
        UnsubscribeInternalAsync(FeedKind.Quote, scripKeys, ScripKeyTranslator.TranslateQuoteOrDepth, cancellationToken);

    /// <summary>Subscribes to five-level depth ticks for the given HIGH scrip keys.</summary>
    /// <exception cref="HighFeedError">A key is not a recognised non-index scrip key (including any key the feed treats as an index — use <see cref="SubscribeIndicesAsync"/> instead), or the subscription would exceed <c>maxScripPerConn</c>.</exception>
    public Task SubscribeDepthAsync(IReadOnlyCollection<string> scripKeys, CancellationToken cancellationToken = default) =>
        SubscribeInternalAsync(FeedKind.Depth, scripKeys, ScripKeyTranslator.TranslateQuoteOrDepth, cancellationToken);

    public Task UnsubscribeDepthAsync(IReadOnlyCollection<string> scripKeys, CancellationToken cancellationToken = default) =>
        UnsubscribeInternalAsync(FeedKind.Depth, scripKeys, ScripKeyTranslator.TranslateQuoteOrDepth, cancellationToken);

    /// <summary>
    /// Subscribes to index ticks. Every key must be present in the committed
    /// index table — a non-index key (or an index the feed does not carry) is
    /// rejected, naming the key, rather than silently resolving to a token the
    /// feed does not recognise.
    /// </summary>
    /// <exception cref="HighFeedError">A key is not a known index key, or the subscription would exceed <c>maxScripPerConn</c>.</exception>
    public Task SubscribeIndicesAsync(IReadOnlyCollection<string> scripKeys, CancellationToken cancellationToken = default) =>
        SubscribeInternalAsync(FeedKind.Index, scripKeys, ScripKeyTranslator.TranslateIndex, cancellationToken);

    public Task UnsubscribeIndicesAsync(IReadOnlyCollection<string> scripKeys, CancellationToken cancellationToken = default) =>
        UnsubscribeInternalAsync(FeedKind.Index, scripKeys, ScripKeyTranslator.TranslateIndex, cancellationToken);

    // ---- Snapshot -------------------------------------------------------------

    /// <summary>
    /// Requests a fresh snapshot for the given keys and returns whatever
    /// merged state is known for them once the server responds (bounded to a
    /// few seconds; on a timeout, returns whatever was already cached rather
    /// than failing outright). A key with no known state yet is omitted.
    /// </summary>
    public Task<IReadOnlyDictionary<string, Quote>> SnapshotQuotesAsync(IReadOnlyCollection<string> scripKeys, CancellationToken cancellationToken = default) =>
        SnapshotInternalAsync(
            FeedKind.Quote, scripKeys, ScripKeyTranslator.TranslateQuoteOrDepth,
            key => _stateMachine.RawQuoteState(key) is { } raw ? FeedStateMachine.BuildQuote(key, raw) : null,
            cancellationToken);

    public Task<IReadOnlyDictionary<string, FeedDepth>> SnapshotDepthAsync(IReadOnlyCollection<string> scripKeys, CancellationToken cancellationToken = default) =>
        SnapshotInternalAsync(
            FeedKind.Depth, scripKeys, ScripKeyTranslator.TranslateQuoteOrDepth,
            key => _stateMachine.RawDepthState(key) is { } raw ? FeedStateMachine.BuildFiveLevelDepth(key, raw) : null,
            cancellationToken);

    public Task<IReadOnlyDictionary<string, IndexQuote>> SnapshotIndicesAsync(IReadOnlyCollection<string> scripKeys, CancellationToken cancellationToken = default) =>
        SnapshotInternalAsync(
            FeedKind.Index, scripKeys, ScripKeyTranslator.TranslateIndex,
            key => _stateMachine.RawIndexState(key) is { } raw ? FeedStateMachine.BuildIndex(key, raw) : null,
            cancellationToken);

    // ---- Connect / auth --------------------------------------------------------

    /// <summary>
    /// Opens a socket, sends the auth frame, and waits for its acknowledgement.
    /// Reads <c>maxScripPerConn</c>/<c>maxScripPerReq</c> off an <c>Ok</c> ack;
    /// throws <see cref="HighFeedAuthError"/> on a <c>NotOk</c> one, having
    /// already disposed the socket — the caller never retries this.
    /// </summary>
    private async Task<FeedSocket> ConnectAndAuthenticateAsync(CancellationToken cancellationToken)
    {
        var socket = new FeedSocket();
        var uri = new Uri(_config.WsBaseUrl);
        _config.Logger?.Info($"HIGH feed connecting {uri}");

        await socket.ConnectAsync(uri, cancellationToken).ConfigureAwait(false);

        // {"type":"cn","sessionid":"<accessToken>"} — deliberately no "mode":
        // it is filled in server-side from the caller's data plan. Never
        // logged: "sessionid" carries the bearer token.
        var authFrame = new JsonObject { ["type"] = "cn", ["sessionid"] = _config.AccessToken };
        await socket.SendAsync(authFrame.ToJsonString(), cancellationToken).ConfigureAwait(false);

        var raw = await socket.ReceiveMessageAsync(cancellationToken).ConfigureAwait(false);
        if (raw is null)
        {
            await socket.DisposeAsync().ConfigureAwait(false);
            throw new HighFeedError("The datafeed socket closed before the auth acknowledgement arrived.");
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(raw);
        }
        catch (JsonException ex)
        {
            await socket.DisposeAsync().ConfigureAwait(false);
            throw new HighFeedError($"The datafeed auth acknowledgement was not valid JSON: {ex.Message}");
        }

        var ack = node as JsonObject ?? (node as JsonArray)?.OfType<JsonObject>().FirstOrDefault();
        if (ack is null)
        {
            await socket.DisposeAsync().ConfigureAwait(false);
            throw new HighFeedError("The datafeed auth acknowledgement was not a recognisable frame.");
        }

        var stat = ack["stat"]?.GetValue<string>();
        var stCode = ack["stCode"]?.GetValue<int>() ?? 0;
        var msg = ack["msg"]?.GetValue<string>() ?? string.Empty;

        if (!string.Equals(stat, "Ok", StringComparison.Ordinal))
        {
            await socket.DisposeAsync().ConfigureAwait(false);
            var reason = HighFeedAuthError.ClassifyReason(stCode, msg);
            _config.Logger?.Error($"HIGH feed auth failed ({stCode}): {msg}");
            throw new HighFeedAuthError(stCode, msg, reason);
        }

        _maxScripPerConn = ack["maxScripPerConn"]?.GetValue<int>() ?? int.MaxValue;
        _maxScripPerReq = ack["maxScripPerReq"]?.GetValue<int>() ?? _maxScripPerConn;
        _config.Logger?.Info(
            $"HIGH feed connected (maxScripPerConn={_maxScripPerConn}, maxScripPerReq={_maxScripPerReq})");

        return socket;
    }

    // ---- Receive loop / reconnect -----------------------------------------------

    private void StartReceiveLoop(FeedSocket socket)
    {
        var cts = new CancellationTokenSource();
        _receiveLoopCts = cts;
        MarkActivity(); // baseline, so the watchdog's clock starts from "just connected" rather than zero
        _ = Task.Run(() => ReceiveLoopAsync(socket, cts.Token));
        _ = Task.Run(() => WatchdogLoopAsync(socket, cts.Token));
    }

    private void MarkActivity() => Interlocked.Exchange(ref _lastActivityTicks, System.Environment.TickCount64);

    private async Task ReceiveLoopAsync(FeedSocket socket, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            string? message;
            string? failure = null;
            try
            {
                message = await socket.ReceiveMessageAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                // A peer that vanishes — an aborted connection, a dropped network, a
                // process killed at the other end — surfaces here as an exception, not as
                // a clean null. Both are the same event to us: the transport is gone. Left
                // uncaught this would escape into the fire-and-forget receive task, be
                // swallowed, and leave a feed that never reconnects and never says why,
                // which is the exact "silently stops delivering" failure this client is
                // supposed to protect against.
                message = null;
                failure = ex.Message;
            }

            if (cancellationToken.IsCancellationRequested) return;

            if (message is null)
            {
                var detail = failure is null ? "closed unexpectedly" : $"failed: {failure}";
                await HandleTransportFailureAsync(socket, detail).ConfigureAwait(false);
                return;
            }

            MarkActivity();

            try
            {
                HandleMessage(message);
            }
            catch (Exception ex)
            {
                _config.Logger?.Error($"HIGH feed: failed to handle a frame: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Independently of the receive loop, notices a peer that has gone away
    /// without ever producing a transport error — a dead-but-open socket, e.g.
    /// after an abrupt server-side close that this platform's TCP stack never
    /// surfaces as a read failure. Polls at a fraction of <see cref="IdleTimeout"/>
    /// so it fires reasonably close to the deadline rather than up to a whole
    /// interval late.
    /// </summary>
    private async Task WatchdogLoopAsync(FeedSocket socket, CancellationToken cancellationToken)
    {
        var idleTimeout = IdleTimeout;
        var checkInterval = TimeSpan.FromMilliseconds(Math.Max(100, idleTimeout.TotalMilliseconds / 5));

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(checkInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (cancellationToken.IsCancellationRequested) return;
            if (!ReferenceEquals(_socket, socket)) return; // superseded by a reconnect already

            var idleForMs = System.Environment.TickCount64 - Interlocked.Read(ref _lastActivityTicks);
            if (idleForMs >= idleTimeout.TotalMilliseconds)
            {
                await HandleTransportFailureAsync(socket, $"went idle (no frame in over {idleTimeout.TotalSeconds:0}s)")
                    .ConfigureAwait(false);
                return;
            }
        }
    }

    /// <summary>
    /// The single path both the receive loop (an explicit read failure) and
    /// the watchdog (an idle timeout, with no read failure at all) funnel
    /// through, so a socket is only ever torn down and reconnected once no
    /// matter which one notices first.
    /// </summary>
    private async Task HandleTransportFailureAsync(FeedSocket socket, string detail)
    {
        lock (_registryLock)
        {
            if (_closed || !ReferenceEquals(_socket, socket) || ReferenceEquals(_failingSocket, socket))
            {
                return;
            }
            _failingSocket = socket;
        }

        _config.Logger?.Warn($"HIGH feed: transport {detail}; will reconnect.");
        OnError?.Invoke(this, new HighFeedError($"The datafeed socket {detail}."));

        await socket.DisposeAsync().ConfigureAwait(false);

        lock (_registryLock)
        {
            if (ReferenceEquals(_socket, socket)) _socket = null;
            if (ReferenceEquals(_failingSocket, socket)) _failingSocket = null;
        }

        if (_closed) return;
        _ = Task.Run(ReconnectLoopAsync);
    }

    /// <summary>
    /// Transport-failure-only reconnection with backoff: re-opens the socket,
    /// re-authenticates, and resubscribes everything that was subscribed
    /// before this client tells the world it is connected again (<see cref="OnReconnected"/>).
    /// An auth failure during reconnection (<see cref="HighFeedAuthError"/>) stops
    /// the loop for good — the server will keep refusing, so retrying it is an
    /// infinite loop against a wall.
    /// </summary>
    private async Task ReconnectLoopAsync()
    {
        var attempt = 0;
        while (!_closed)
        {
            attempt++;
            var backoffMs = Math.Min(200 * (1 << Math.Min(attempt - 1, 7)), 10_000) + Random.Shared.Next(0, 100);
            try
            {
                await Task.Delay(backoffMs).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (_closed) return;

            await _connectLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var socket = await ConnectAndAuthenticateAsync(CancellationToken.None).ConfigureAwait(false);
                _socket = socket;
                StartReceiveLoop(socket);

                await ResubscribeAllAsync(CancellationToken.None).ConfigureAwait(false);

                _config.Logger?.Info("HIGH feed reconnected and resubscribed.");
                OnReconnected?.Invoke(this, EventArgs.Empty);
                return;
            }
            catch (HighFeedAuthError authError)
            {
                _config.Logger?.Error(
                    $"HIGH feed reconnect stopped — auth failed ({authError.StCode}): {authError.Message}");
                OnError?.Invoke(this, authError);
                return;
            }
            catch (Exception ex)
            {
                _config.Logger?.Warn($"HIGH feed reconnect attempt {attempt} failed: {ex.Message}");
            }
            finally
            {
                _connectLock.Release();
            }
        }
    }

    private async Task ResubscribeAllAsync(CancellationToken cancellationToken)
    {
        Dictionary<FeedKind, List<string>> snapshot;
        lock (_registryLock)
        {
            snapshot = _subscribedByKind.ToDictionary(kv => kv.Key, kv => kv.Value.ToList());
        }

        foreach (var (kind, keys) in snapshot)
        {
            if (keys.Count == 0) continue;

            Func<string, (string Segment, string Identifier)> translate = kind == FeedKind.Index
                ? ScripKeyTranslator.TranslateIndex
                : ScripKeyTranslator.TranslateQuoteOrDepth;
            var translated = keys.Select(key => (ScripKey: key, Feed: translate(key))).ToList();

            foreach (var chunk in translated.Chunk(Math.Max(1, _maxScripPerReq)))
            {
                var scrips = string.Join("&", chunk.Select(t => $"{t.Feed.Segment}|{t.Feed.Identifier}"));
                await SendControlRequestAsync($"{kind.WirePrefix()}s", scrips, kind, cancellationToken).ConfigureAwait(false);

                lock (_registryLock)
                {
                    foreach (var t in chunk) _feedIdToScripKey[$"{t.Feed.Segment}|{t.Feed.Identifier}"] = t.ScripKey;
                }
            }
        }
    }

    // ---- Frame handling -----------------------------------------------------

    private void HandleMessage(string raw)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(raw);
        }
        catch (JsonException ex)
        {
            _config.Logger?.Error($"HIGH feed: could not parse a frame: {ex.Message}");
            return;
        }

        if (node is JsonArray array && IsControlAck(array))
        {
            _pendingControlAck?.TrySetResult(array);
            return;
        }

        foreach (var tick in AsTickObjects(node))
        {
            DispatchTick(tick);
        }
    }

    private static bool IsControlAck(JsonArray array) =>
        array.Count > 0 && array[0] is JsonObject obj && obj["type"]?.GetValue<string>() is "sub" or "unsub";

    private static IEnumerable<JsonObject> AsTickObjects(JsonNode? node) => node switch
    {
        JsonArray array => array.OfType<JsonObject>(),
        JsonObject obj => new[] { obj },
        _ => Array.Empty<JsonObject>(),
    };

    private void DispatchTick(JsonObject tick)
    {
        var exchange = FeedFieldMaps.GetString(tick, "e");
        var token = FeedFieldMaps.GetString(tick, "tk");
        if (exchange is null || token is null) return;

        string? scripKey;
        lock (_registryLock)
        {
            _feedIdToScripKey.TryGetValue($"{exchange}|{token}", out scripKey);
        }

        // Not one of ours (stale unsubscribe, or a frame that arrived before a
        // pending unsubscribe's ack) — dropped rather than guessed at.
        if (scripKey is null) return;

        _stateMachine.Dispatch(scripKey, tick);
    }

    private void SignalPendingSnapshot(string scripKey)
    {
        foreach (var kind in StaticFeedKinds)
        {
            TaskCompletionSource<bool>? tcs;
            lock (_registryLock)
            {
                _pendingSnapshots.TryGetValue(PendingKey(kind, scripKey), out tcs);
            }
            tcs?.TrySetResult(true);
        }
    }

    private static readonly FeedKind[] StaticFeedKinds = { FeedKind.Quote, FeedKind.Depth, FeedKind.Index };

    private static string PendingKey(FeedKind kind, string scripKey) => $"{kind}:{scripKey}";

    // ---- Subscribe / Unsubscribe / Snapshot plumbing --------------------------

    private async Task SubscribeInternalAsync(
        FeedKind kind,
        IReadOnlyCollection<string> scripKeys,
        Func<string, (string Segment, string Identifier)> translate,
        CancellationToken cancellationToken)
    {
        EnsureConnected();
        if (scripKeys.Count == 0) return;

        var translated = scripKeys.Select(key => (ScripKey: key, Feed: translate(key))).ToList();

        lock (_registryLock)
        {
            var set = GetOrAddSet(kind);
            var newCount = translated.Count(t => !set.Contains(t.ScripKey));
            var currentTotal = _subscribedByKind.Values.Sum(s => s.Count);
            var projectedTotal = currentTotal + newCount;
            if (projectedTotal > _maxScripPerConn)
            {
                throw new HighFeedError(
                    $"Subscribing {translated.Count} scrip(s) to {kind} would bring this connection to " +
                    $"{projectedTotal}, over maxScripPerConn ({_maxScripPerConn}). Currently subscribed: {currentTotal}.");
            }
        }

        foreach (var chunk in translated.Chunk(Math.Max(1, _maxScripPerReq)))
        {
            var scrips = string.Join("&", chunk.Select(t => $"{t.Feed.Segment}|{t.Feed.Identifier}"));
            await SendControlRequestAsync($"{kind.WirePrefix()}s", scrips, kind, cancellationToken).ConfigureAwait(false);

            lock (_registryLock)
            {
                var set = GetOrAddSet(kind);
                foreach (var t in chunk)
                {
                    set.Add(t.ScripKey);
                    _feedIdToScripKey[$"{t.Feed.Segment}|{t.Feed.Identifier}"] = t.ScripKey;
                }
            }
        }
    }

    private async Task UnsubscribeInternalAsync(
        FeedKind kind,
        IReadOnlyCollection<string> scripKeys,
        Func<string, (string Segment, string Identifier)> translate,
        CancellationToken cancellationToken)
    {
        EnsureConnected();
        if (scripKeys.Count == 0) return;

        var translated = scripKeys.Select(key => (ScripKey: key, Feed: translate(key))).ToList();

        foreach (var chunk in translated.Chunk(Math.Max(1, _maxScripPerReq)))
        {
            var scrips = string.Join("&", chunk.Select(t => $"{t.Feed.Segment}|{t.Feed.Identifier}"));
            await SendControlRequestAsync($"{kind.WirePrefix()}u", scrips, kind, cancellationToken).ConfigureAwait(false);

            lock (_registryLock)
            {
                var set = GetOrAddSet(kind);
                foreach (var t in chunk)
                {
                    set.Remove(t.ScripKey);
                    _feedIdToScripKey.Remove($"{t.Feed.Segment}|{t.Feed.Identifier}");
                }
            }
        }
    }

    private async Task<IReadOnlyDictionary<string, T>> SnapshotInternalAsync<T>(
        FeedKind kind,
        IReadOnlyCollection<string> scripKeys,
        Func<string, (string Segment, string Identifier)> translate,
        Func<string, T?> read,
        CancellationToken cancellationToken)
        where T : class
    {
        EnsureConnected();
        var result = new Dictionary<string, T>(StringComparer.Ordinal);
        if (scripKeys.Count == 0) return result;

        var translated = scripKeys.Select(key => (ScripKey: key, Feed: translate(key))).ToList();
        var waiters = new List<TaskCompletionSource<bool>>(translated.Count);

        lock (_registryLock)
        {
            foreach (var t in translated)
            {
                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _pendingSnapshots[PendingKey(kind, t.ScripKey)] = tcs;
                waiters.Add(tcs);
            }
        }

        try
        {
            foreach (var chunk in translated.Chunk(Math.Max(1, _maxScripPerReq)))
            {
                var scrips = string.Join("&", chunk.Select(t => $"{t.Feed.Segment}|{t.Feed.Identifier}"));
                await SendControlRequestAsync($"{kind.WirePrefix()}sp", scrips, kind, cancellationToken).ConfigureAwait(false);
            }

            // A bounded window for the server to push fresh data back; on a
            // timeout, fall through and return whatever is already cached
            // rather than failing a caller who is already subscribed.
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            try
            {
                await Task.WhenAll(waiters.Select(w => w.Task)).WaitAsync(linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Timed out waiting for fresh data — use cached state below.
            }
        }
        finally
        {
            lock (_registryLock)
            {
                foreach (var t in translated) _pendingSnapshots.Remove(PendingKey(kind, t.ScripKey));
            }
        }

        foreach (var t in translated)
        {
            if (read(t.ScripKey) is { } value) result[t.ScripKey] = value;
        }

        return result;
    }

    private async Task SendControlRequestAsync(string type, string scrips, FeedKind kind, CancellationToken cancellationToken)
    {
        await _requestLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var socket = _socket ?? throw new InvalidOperationException("HighFeed is not connected. Call ConnectAsync first.");
            var tcs = new TaskCompletionSource<JsonArray>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingControlAck = tcs;

            var frame = new JsonObject
            {
                ["type"] = type,
                ["scrips"] = scrips,
                ["channelnum"] = kind.ChannelNumber(),
            };

            _config.Logger?.Debug($"HIGH feed -> {type} channel {kind.ChannelNumber()}");
            await socket.SendAsync(frame.ToJsonString(), cancellationToken).ConfigureAwait(false);

            await using var registration = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
            var array = await tcs.Task.ConfigureAwait(false);
            _pendingControlAck = null;

            var ack = array.OfType<JsonObject>().FirstOrDefault();
            var stat = ack?["stat"]?.GetValue<string>();
            if (!string.Equals(stat, "Ok", StringComparison.Ordinal))
            {
                var stCode = ack?["stCode"]?.GetValue<int>() ?? 0;
                var msg = ack?["msg"]?.GetValue<string>() ?? "the server refused the request";
                throw new HighFeedError($"HIGH feed \"{type}\" was refused ({stCode}): {msg}");
            }
        }
        finally
        {
            _requestLock.Release();
        }
    }

    private HashSet<string> GetOrAddSet(FeedKind kind)
    {
        if (!_subscribedByKind.TryGetValue(kind, out var set))
        {
            set = new HashSet<string>(StringComparer.Ordinal);
            _subscribedByKind[kind] = set;
        }
        return set;
    }

    private void EnsureConnected()
    {
        ThrowIfClosed();
        if (_socket is null) throw new InvalidOperationException("HighFeed is not connected. Call ConnectAsync first.");
    }

    private void ThrowIfClosed()
    {
        if (_closed) throw new InvalidOperationException("This HighFeed has been closed.");
    }
}
