// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

// Streams quotes for a couple of scrips and an index. Run with:
//   HIGH_ACCESS_TOKEN=... dotnet run --project examples/LiveDatafeed

using High.OpenApi;
using High.OpenApi.Feed;

await using var feed = new HighFeed(new HighClientOptions());

feed.OnError += (_, ex) => Console.Error.WriteLine($"[feed error] {ex.Message}");
feed.OnReconnected += (_, _) => Console.WriteLine("[feed] reconnected and resubscribed");

try
{
    await feed.ConnectAsync();
    await feed.SubscribeQuotesAsync(new[] { "NSE@2885", "BSE@532540" });
    await feed.SubscribeIndicesAsync(new[] { "NSE@26000" });

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    await foreach (var evt in feed.Events(cts.Token))
    {
        switch (evt)
        {
            case QuoteEvent q:
                Console.WriteLine($"{q.ScripKey,-14} ltp={q.Quote.LastTradedPrice} changed=[{string.Join(",", q.ChangedFields)}]");
                break;
            case DepthEvent d:
                var top = d.Depth.Bids[0];
                Console.WriteLine($"{d.ScripKey,-14} {d.Depth.Source} bestBid={top.Price}x{top.Quantity}");
                break;
            case IndexEvent i:
                Console.WriteLine($"{i.ScripKey,-14} {i.Index.IndexName}={i.Index.IndexValue}");
                break;
        }
    }
}
catch (OperationCanceledException)
{
    // The 30-second demo window elapsed.
}
catch (HighFeedAuthError error)
{
    Console.Error.WriteLine($"[{error.StCode}] {error.Reason}: {error.ServerMessage}");
    System.Environment.Exit(1);
}
