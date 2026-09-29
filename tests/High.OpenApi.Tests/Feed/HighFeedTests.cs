// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using System.Net.WebSockets;
using System.Text.Json.Nodes;
using High.OpenApi;
using High.OpenApi.Feed;
using High.OpenApi.Logging;
using Xunit;

namespace High.OpenApi.Tests.Feed;

file sealed class CapturingSink : ILogSink
{
    public List<string> Lines { get; } = new();
    private readonly object _lock = new();
    public void Error(string message, object? detail = null) => Add(message, detail);
    public void Warn(string message, object? detail = null) => Add(message, detail);
    public void Info(string message, object? detail = null) => Add(message, detail);
    public void Debug(string message, object? detail = null) => Add(message, detail);

    private void Add(string message, object? detail)
    {
        lock (_lock) Lines.Add(detail is null ? message : $"{message} {detail}");
    }
}

public class HighFeedTests
{
    private static HighClientOptions OptionsFor(FeedTestServer server, string accessToken = "tok", ILogSink? sink = null) => new()
    {
        Environment = Environment.Production,
        AccessToken = accessToken,
        WsBaseUrl = server.WsUrl,
        LogLevel = sink is null ? LogLevel.Silent : LogLevel.Debug,
        LogSink = sink,
    };

    private static string TypeOf(string json) => JsonNode.Parse(json)!["type"]!.GetValue<string>();

    // ---- Sandbox refused at construction --------------------------------------

    [Fact]
    public void Sandbox_environment_is_refused_at_construction()
    {
        var ex = Assert.Throws<HighFeedError>(() =>
            new HighFeed(new HighClientOptions { Environment = Environment.Sandbox, AccessToken = "tok" }));
        Assert.Contains("sandbox", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_the_default_is_accepted_at_construction()
    {
        var feed = new HighFeed(new HighClientOptions { AccessToken = "tok" });
        Assert.NotNull(feed);
    }

    // ---- Auth-before-subscribe ordering ----------------------------------------

    [Fact]
    public async Task ConnectAsync_sends_cn_first_and_nothing_else_reaches_the_server_before_its_ack()
    {
        var seenTypes = new List<string>();
        var done = new TaskCompletionSource();

        await using var server = new FeedTestServer(async (socket, ct) =>
        {
            var auth = await FeedTestServerFrames.ReceiveTextAsync(socket, ct);
            seenTypes.Add(TypeOf(auth));
            await FeedTestServerFrames.SendTextAsync(socket, FeedTestServerFrames.OkAck, ct);

            var sub = await FeedTestServerFrames.ReceiveTextAsync(socket, ct);
            seenTypes.Add(TypeOf(sub));
            await FeedTestServerFrames.SendTextAsync(socket, FeedTestServerFrames.SubOkAck, ct);
            done.TrySetResult();
        });

        await using var feed = new HighFeed(OptionsFor(server));
        await feed.ConnectAsync().WaitAsync(TimeSpan.FromSeconds(10));
        await feed.SubscribeQuotesAsync(new[] { "NSE@2885" }).WaitAsync(TimeSpan.FromSeconds(10));

        await done.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(new[] { "cn", "mws" }, seenTypes);
    }

    // ---- NotOk auth: typed error, never retried --------------------------------

    [Fact]
    public async Task NotOk_auth_throws_a_typed_error_and_is_never_retried()
    {
        var connectionCount = 0;
        await using var server = new FeedTestServer(async (socket, ct) =>
        {
            Interlocked.Increment(ref connectionCount);
            await FeedTestServerFrames.ReceiveTextAsync(socket, ct);
            await FeedTestServerFrames.SendTextAsync(socket, FeedTestServerFrames.NotOkAck(11002, "invalid field count"), ct);
        });

        await using var feed = new HighFeed(OptionsFor(server));

        var ex = await Assert.ThrowsAsync<HighFeedAuthError>(() => feed.ConnectAsync().WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(11002, ex.StCode);
        Assert.Equal("invalid field count", ex.ServerMessage);
        Assert.Equal(FeedAuthFailureReason.MalformedRequest, ex.Reason);

        // No retry: give any errant retry loop a moment, then confirm only one
        // connection ever reached the server.
        await Task.Delay(300);
        Assert.Equal(1, connectionCount);
    }

    [Fact]
    public async Task NotOk_auth_classifies_a_missing_data_plan_message()
    {
        await using var server = new FeedTestServer(async (socket, ct) =>
        {
            await FeedTestServerFrames.ReceiveTextAsync(socket, ct);
            await FeedTestServerFrames.SendTextAsync(socket, FeedTestServerFrames.NotOkAck(11001, "no active data plan subscription"), ct);
        });

        await using var feed = new HighFeed(OptionsFor(server));
        var ex = await Assert.ThrowsAsync<HighFeedAuthError>(() => feed.ConnectAsync().WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(FeedAuthFailureReason.MissingDataPlan, ex.Reason);
    }

    [Fact]
    public async Task NotOk_auth_classifies_an_invalid_token_message()
    {
        await using var server = new FeedTestServer(async (socket, ct) =>
        {
            await FeedTestServerFrames.ReceiveTextAsync(socket, ct);
            await FeedTestServerFrames.SendTextAsync(socket, FeedTestServerFrames.NotOkAck(11001, "invalid or expired session token"), ct);
        });

        await using var feed = new HighFeed(OptionsFor(server));
        var ex = await Assert.ThrowsAsync<HighFeedAuthError>(() => feed.ConnectAsync().WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(FeedAuthFailureReason.InvalidToken, ex.Reason);
    }

    // ---- Both limits honoured ---------------------------------------------------

    [Fact]
    public async Task Subscribe_splits_a_request_across_maxScripPerReq_and_honours_maxScripPerConn()
    {
        var subFrames = new List<JsonNode>();
        var allReceived = new TaskCompletionSource();

        await using var server = new FeedTestServer(async (socket, ct) =>
        {
            await FeedTestServerFrames.ReceiveTextAsync(socket, ct);
            await FeedTestServerFrames.SendTextAsync(socket, FeedTestServerFrames.OkAckWithLimits(maxScripPerConn: 3, maxScripPerReq: 2), ct);

            for (var i = 0; i < 2; i++)
            {
                var sub = await FeedTestServerFrames.ReceiveTextAsync(socket, ct);
                subFrames.Add(JsonNode.Parse(sub)!);
                await FeedTestServerFrames.SendTextAsync(socket, FeedTestServerFrames.SubOkAck, ct);
            }
            allReceived.TrySetResult();
        });

        await using var feed = new HighFeed(OptionsFor(server));
        await feed.ConnectAsync().WaitAsync(TimeSpan.FromSeconds(10));

        // 3 scrips, maxScripPerReq = 2: must arrive as two requests (2 then 1).
        await feed.SubscribeQuotesAsync(new[] { "NSE@1", "NSE@2", "NSE@3" }).WaitAsync(TimeSpan.FromSeconds(10));
        await allReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(2, subFrames.Count);
        var counts = subFrames
            .Select(f => f["scrips"]!.GetValue<string>().Split('&').Length)
            .OrderDescending()
            .ToList();
        Assert.Equal(new[] { 2, 1 }, counts);

        // A 4th scrip would bring the connection to 4, over maxScripPerConn (3).
        var ex = await Assert.ThrowsAsync<HighFeedError>(() => feed.SubscribeQuotesAsync(new[] { "NSE@4" }).WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Contains("maxScripPerConn", ex.Message);
    }

    // ---- Delta merge, quote/depth split, level pairing -------------------------

    [Fact]
    public async Task Ticks_are_merged_as_deltas_into_a_complete_snapshot()
    {
        await using var server = SubscribedServer(out var pushTick, out var subscribedSignal);
        await using var feed = new HighFeed(OptionsFor(server));
        await feed.ConnectAsync().WaitAsync(TimeSpan.FromSeconds(10));

        var events = new List<QuoteEvent>();
        feed.OnQuote += (_, e) => events.Add(e);

        await feed.SubscribeQuotesAsync(new[] { "NSE@2885" }).WaitAsync(TimeSpan.FromSeconds(10));
        await subscribedSignal.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await pushTick("[{\"name\":\"sf\",\"e\":\"nse_cm\",\"tk\":\"2885\",\"ltp\":\"1905.65\",\"v\":\"1000\"}]");
        await WaitForCount(events, 1);

        await pushTick("[{\"name\":\"sf\",\"e\":\"nse_cm\",\"tk\":\"2885\",\"c\":\"1859.05\",\"cng\":\"46.60\",\"nc\":\"2.51\"}]");
        await WaitForCount(events, 2);

        var latest = events[^1].Quote;
        // ltp/v came from the first, older delta — a correct merge must still
        // carry them even though the second delta never repeated them.
        Assert.Equal(1905.65m, latest.LastTradedPrice);
        Assert.Equal(1000L, latest.Volume);
        Assert.Equal(1859.05m, latest.PreviousClose);
        // cng is absolute change, nc is the percentage — the opposite of the
        // vendor sample's own labels; the arithmetic (46.60 / 1859.05 ~= 2.51%) settles it.
        Assert.Equal(46.60m, latest.Change);
        Assert.Equal(2.51m, latest.ChangePercent);

        Assert.Contains(nameof(Quote.PreviousClose), events[^1].ChangedFields);
        Assert.DoesNotContain(nameof(Quote.LastTradedPrice), events[^1].ChangedFields);
    }

    [Fact]
    public async Task A_FULL_market_watch_tick_raises_a_quote_event_and_a_one_level_depth_event_separately()
    {
        await using var server = SubscribedServer(out var pushTick, out var subscribedSignal);
        await using var feed = new HighFeed(OptionsFor(server));
        await feed.ConnectAsync().WaitAsync(TimeSpan.FromSeconds(10));

        var quotes = new List<QuoteEvent>();
        var depths = new List<DepthEvent>();
        feed.OnQuote += (_, e) => quotes.Add(e);
        feed.OnDepth += (_, e) => depths.Add(e);

        await feed.SubscribeQuotesAsync(new[] { "NSE@2885" }).WaitAsync(TimeSpan.FromSeconds(10));
        await subscribedSignal.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await pushTick("[{\"name\":\"sf\",\"e\":\"nse_cm\",\"tk\":\"2885\",\"ltp\":\"1905.65\",\"bp\":\"1905.00\",\"bq\":\"10\",\"sp\":\"1906.00\",\"bs\":\"20\"}]");
        await WaitForCount(quotes, 1);
        await WaitForCount(depths, 1);

        Assert.Equal(1905.65m, quotes[0].Quote.LastTradedPrice);

        var depth = depths[0].Depth;
        Assert.Equal(FeedDepthSource.MarketWatch, depth.Source);
        Assert.Equal(1, depth.LevelCount);
        Assert.Single(depth.Bids);
        Assert.Single(depth.Asks);
        Assert.Equal(1905.00m, depth.Bids[0].Price);
        Assert.Equal(10L, depth.Bids[0].Quantity);
        Assert.Null(depth.Bids[0].OrderCount);
        Assert.Equal(1906.00m, depth.Asks[0].Price);
        Assert.Equal(20L, depth.Asks[0].Quantity);

        // A tick that only moves ltp raises a quote event and no depth event.
        await pushTick("[{\"name\":\"sf\",\"e\":\"nse_cm\",\"tk\":\"2885\",\"ltp\":\"1906.10\"}]");
        await WaitForCount(quotes, 2);
        await Task.Delay(150);
        Assert.Single(depths);
    }

    [Fact]
    public async Task Five_level_depth_pairs_price_and_quantity_suffixes_with_the_order_count_offset_by_one()
    {
        await using var server = SubscribedServer(out var pushTick, out var subscribedSignal, prefix: "dp");
        await using var feed = new HighFeed(OptionsFor(server));
        await feed.ConnectAsync().WaitAsync(TimeSpan.FromSeconds(10));

        var depths = new List<DepthEvent>();
        feed.OnDepth += (_, e) => depths.Add(e);

        await feed.SubscribeDepthAsync(new[] { "NSE@2885" }).WaitAsync(TimeSpan.FromSeconds(10));
        await subscribedSignal.Task.WaitAsync(TimeSpan.FromSeconds(5));

        const string tick = "[{\"name\":\"dp\",\"e\":\"nse_cm\",\"tk\":\"2885\"," +
            "\"bp\":\"100.1\",\"bp1\":\"100.2\",\"bp2\":\"100.3\",\"bp3\":\"100.4\",\"bp4\":\"100.5\"," +
            "\"bq\":\"1\",\"bq1\":\"2\",\"bq2\":\"3\",\"bq3\":\"4\",\"bq4\":\"5\"," +
            "\"bno1\":\"11\",\"bno2\":\"12\",\"bno3\":\"13\",\"bno4\":\"14\",\"bno5\":\"15\"," +
            "\"sp\":\"200.1\",\"sp1\":\"200.2\",\"sp2\":\"200.3\",\"sp3\":\"200.4\",\"sp4\":\"200.5\"," +
            "\"bs\":\"6\",\"bs1\":\"7\",\"bs2\":\"8\",\"bs3\":\"9\",\"bs4\":\"10\"," +
            "\"sno1\":\"21\",\"sno2\":\"22\",\"sno3\":\"23\",\"sno4\":\"24\",\"sno5\":\"25\"}]";
        await pushTick(tick);
        await WaitForCount(depths, 1);

        var depth = depths[0].Depth;
        Assert.Equal(FeedDepthSource.Depth, depth.Source);
        Assert.Equal(5, depth.LevelCount);

        // Level 1 pairs the unnumbered bp/bq with bno1; level 5 pairs bp4/bq4 with bno5.
        Assert.Equal(100.1m, depth.Bids[0].Price);
        Assert.Equal(1L, depth.Bids[0].Quantity);
        Assert.Equal(11L, depth.Bids[0].OrderCount);

        Assert.Equal(100.5m, depth.Bids[4].Price);
        Assert.Equal(5L, depth.Bids[4].Quantity);
        Assert.Equal(15L, depth.Bids[4].OrderCount);

        Assert.Equal(200.1m, depth.Asks[0].Price);
        Assert.Equal(6L, depth.Asks[0].Quantity);
        Assert.Equal(21L, depth.Asks[0].OrderCount);

        Assert.Equal(200.5m, depth.Asks[4].Price);
        Assert.Equal(10L, depth.Asks[4].Quantity);
        Assert.Equal(25L, depth.Asks[4].OrderCount);
    }

    // ---- Partial frame reassembly ------------------------------------------------

    [Fact]
    public async Task A_large_tick_split_across_several_WebSocket_frames_is_reassembled_before_parsing()
    {
        var subscribedSignal = new TaskCompletionSource();
        WebSocket? serverSocket = null;

        await using var server = new FeedTestServer(async (socket, ct) =>
        {
            serverSocket = socket;
            await FeedTestServerFrames.ReceiveTextAsync(socket, ct);
            await FeedTestServerFrames.SendTextAsync(socket, FeedTestServerFrames.OkAck, ct);

            await FeedTestServerFrames.ReceiveTextAsync(socket, ct);
            await FeedTestServerFrames.SendTextAsync(socket, FeedTestServerFrames.SubOkAck, ct);
            subscribedSignal.TrySetResult();

            // Keep the handler alive long enough for the test to push a chunked tick.
            await Task.Delay(3000, ct).ContinueWith(_ => { }, TaskScheduler.Default);
        });

        await using var feed = new HighFeed(OptionsFor(server));
        await feed.ConnectAsync().WaitAsync(TimeSpan.FromSeconds(10));

        var quotes = new List<QuoteEvent>();
        feed.OnQuote += (_, e) => quotes.Add(e);

        await feed.SubscribeQuotesAsync(new[] { "NSE@2885" }).WaitAsync(TimeSpan.FromSeconds(10));
        await subscribedSignal.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.NotNull(serverSocket);
        // A long trading symbol pads the payload so it must span several
        // small WebSocket frames before EndOfMessage.
        var padding = new string('A', 500);
        var tick = $"[{{\"name\":\"sf\",\"e\":\"nse_cm\",\"tk\":\"2885\",\"ts\":\"{padding}\",\"ltp\":\"12.34\"}}]";
        await FeedTestServerFrames.SendTextInChunksAsync(serverSocket!, tick, chunkSize: 37, CancellationToken.None);

        await WaitForCount(quotes, 1);
        Assert.Equal(padding, quotes[0].Quote.TradingSymbol);
        Assert.Equal(12.34m, quotes[0].Quote.LastTradedPrice);
    }

    // ---- Reconnect and resubscribe ----------------------------------------------

    [Fact]
    public async Task A_transport_failure_triggers_reconnect_reauth_and_resubscribe_before_reporting_connected()
    {
        var connectionCount = 0;
        var secondConnectionSubscribed = new TaskCompletionSource();
        var secondConnectionSubFrame = new TaskCompletionSource<string>();

        await using var server = new FeedTestServer(async (socket, ct) =>
        {
            var n = Interlocked.Increment(ref connectionCount);
            await FeedTestServerFrames.ReceiveTextAsync(socket, ct);
            await FeedTestServerFrames.SendTextAsync(socket, FeedTestServerFrames.OkAck, ct);

            var sub = await FeedTestServerFrames.ReceiveTextAsync(socket, ct);

            if (n == 1)
            {
                await FeedTestServerFrames.SendTextAsync(socket, FeedTestServerFrames.SubOkAck, ct);
                // Simulate a transport failure: close abruptly without a
                // WebSocket close handshake.
                socket.Abort();
                return;
            }

            secondConnectionSubFrame.TrySetResult(sub);
            await FeedTestServerFrames.SendTextAsync(socket, FeedTestServerFrames.SubOkAck, ct);
            secondConnectionSubscribed.TrySetResult();
        });

        await using var feed = new HighFeed(OptionsFor(server))
        {
            // A server-side Abort() does not reliably surface as a read
            // failure on every platform/loopback stack — the whole point of
            // the idle watchdog is to notice a dead-but-open socket even when
            // the transport itself never errors. Shortened so the test does
            // not have to wait out the (much longer) production default.
            IdleTimeout = TimeSpan.FromSeconds(2),
        };
        var reconnected = new TaskCompletionSource();
        feed.OnReconnected += (_, _) => reconnected.TrySetResult();

        await feed.ConnectAsync().WaitAsync(TimeSpan.FromSeconds(10));
        await feed.SubscribeQuotesAsync(new[] { "NSE@2885" }).WaitAsync(TimeSpan.FromSeconds(10));

        await reconnected.Task.WaitAsync(TimeSpan.FromSeconds(20));
        await secondConnectionSubscribed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(2, connectionCount);
        var resubscribeFrame = await secondConnectionSubFrame.Task;
        Assert.Contains("2885", resubscribeFrame);
    }

    // ---- Credentials never logged -----------------------------------------------

    [Fact]
    public async Task The_access_token_never_appears_in_any_log_line()
    {
        var sink = new CapturingSink();
        await using var server = SubscribedServer(out var pushTick, out var subscribedSignal);
        await using var feed = new HighFeed(OptionsFor(server, accessToken: "TOP-SECRET-TOKEN", sink: sink));
        await feed.ConnectAsync().WaitAsync(TimeSpan.FromSeconds(10));
        await feed.SubscribeQuotesAsync(new[] { "NSE@2885" }).WaitAsync(TimeSpan.FromSeconds(10));
        await subscribedSignal.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await pushTick("[{\"name\":\"sf\",\"e\":\"nse_cm\",\"tk\":\"2885\",\"ltp\":\"1.00\"}]");
        await Task.Delay(150);

        Assert.DoesNotContain(sink.Lines, line => line.Contains("TOP-SECRET-TOKEN"));
    }

    [Fact]
    public void The_access_token_never_appears_in_ToString_of_HighFeed_or_its_events()
    {
        var feed = new HighFeed(new HighClientOptions { AccessToken = "TOP-SECRET-TOKEN" });
        Assert.DoesNotContain("TOP-SECRET-TOKEN", feed.ToString());

        var evt = new QuoteEvent
        {
            ScripKey = "NSE@2885",
            ChangedFields = Array.Empty<string>(),
            Quote = new Quote { ScripKey = "NSE@2885" },
        };
        Assert.DoesNotContain("TOP-SECRET-TOKEN", evt.ToString());
    }

    // ---- Test helpers -------------------------------------------------------------

    /// <summary>A server that completes the cn+sub handshake for a single subscribe call and then lets the test push further tick frames.</summary>
    private static FeedTestServer SubscribedServer(out Func<string, Task> pushTick, out TaskCompletionSource subscribedSignal, string prefix = "mw")
    {
        var signal = new TaskCompletionSource();
        WebSocket? serverSocket = null;
        var socketReady = new TaskCompletionSource();

        var server = new FeedTestServer(async (socket, ct) =>
        {
            serverSocket = socket;
            socketReady.TrySetResult();

            await FeedTestServerFrames.ReceiveTextAsync(socket, ct);
            await FeedTestServerFrames.SendTextAsync(socket, FeedTestServerFrames.OkAck, ct);

            var sub = await FeedTestServerFrames.ReceiveTextAsync(socket, ct);
            Assert.Equal(prefix + "s", TypeOf(sub));
            await FeedTestServerFrames.SendTextAsync(socket, FeedTestServerFrames.SubOkAck, ct);
            signal.TrySetResult();

            await Task.Delay(5000, ct).ContinueWith(_ => { }, TaskScheduler.Default);
        });

        subscribedSignal = signal;
        pushTick = async json =>
        {
            await socketReady.Task.ConfigureAwait(false);
            await FeedTestServerFrames.SendTextAsync(serverSocket!, json, CancellationToken.None).ConfigureAwait(false);
        };
        return server;
    }

    private static async Task WaitForCount<T>(List<T> list, int count)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (list.Count < count && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }
        Assert.True(list.Count >= count, $"expected at least {count} item(s), got {list.Count}");
    }
}
