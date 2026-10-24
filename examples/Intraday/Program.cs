// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

// Reads the candles of the most recent trading session, twice: once naming the
// instrument by trading symbol, once by exchange segment and scrip code. Both
// name the same instrument. Needs a data plan. Run with:
//   HIGH_ACCESS_TOKEN=... dotnet run --project examples/Intraday
//
// Read-only, so a failure is safe to retry by hand. The SDK itself never retries a POST.

using High.OpenApi;
using High.OpenApi.Models;

using var high = new HighClient();

try
{
    // Form 1: by trading symbol from the instrument list.
    var bySymbol = await high.Scrips.IntradayAsync(new IntradayRequest
    {
        TradingSymbol = "RELIANCE-EQ",
        Interval = IntradayInterval.FiveMinutes,
    });
    PrintLastCandles("RELIANCE-EQ by trading symbol", bySymbol, count: 5);

    // Form 2: by exchange segment and scrip code. Send both, and leave TradingSymbol unset.
    var byCode = await high.Scrips.IntradayAsync(new IntradayRequest
    {
        ExchangeSegment = ExchangeSegment.Nse,
        ScripCode = "2885",
        Interval = IntradayInterval.FiveMinutes,
    });
    PrintLastCandles("NSE 2885 by segment and scrip code", byCode, count: 5);
}
catch (HighApiError error)
{
    Console.Error.WriteLine($"[{error.Status}] {error.Code ?? "no code"}: {error.Message}");
    if (error.RequestId is not null) Console.Error.WriteLine($"requestId: {error.RequestId}");
    System.Environment.Exit(1);
}

// The response is columnar: index i across the arrays is one candle.
static void PrintLastCandles(string title, HistoricalCandles candles, int count)
{
    Console.WriteLine($"{title}: {candles.Timestamp.Count} candles at {candles.Interval}");

    var first = Math.Max(0, candles.Timestamp.Count - count);
    for (var i = first; i < candles.Timestamp.Count; i++)
    {
        var time = DateTimeOffset.FromUnixTimeSeconds(candles.Timestamp[i]).UtcDateTime;
        Console.WriteLine(
            $"  {time:yyyy-MM-dd HH:mm} UTC  o={candles.Open[i]} h={candles.High[i]} " +
            $"l={candles.Low[i]} c={candles.Close[i]} v={candles.Volume[i]}");
    }
}
