// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

// Streams the equity instrument list and prints a running count plus the
// first few rows. No credentials required. Run with:
//   dotnet run --project examples/InstrumentList

using High.OpenApi;
using High.OpenApi.Models;

using var high = new HighClient();

try
{
    var count = 0;
    await foreach (var instrument in high.Instruments.Stream(InstrumentCategory.Equity))
    {
        count++;
        if (count <= 5)
        {
            Console.WriteLine($"{instrument.TradingSymbol,-20} {instrument.Name} (lot {instrument.LotSize}, tick {instrument.PriceTick})");
        }
    }

    Console.WriteLine($"...{count} equity instruments total.");

    // Cache this yourself and re-download once a day — the list is rebuilt
    // once each trading morning and does not change intraday.
}
catch (HighApiError error)
{
    Console.Error.WriteLine($"[{error.Status}] {error.Code ?? "no code"}: {error.Message}");
    if (error.RequestId is not null) Console.Error.WriteLine($"requestId: {error.RequestId}");
    System.Environment.Exit(1);
}
