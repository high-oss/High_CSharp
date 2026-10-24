// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

// Sets a stop-loss and then a target on an open intraday long position. Both
// place a new exit order, or amend the existing one. Needs a registered static IP.
//
//   HIGH_ENVIRONMENT=sandbox HIGH_ACCESS_TOKEN=... dotnet run --project examples/PositionRisk
//
// This changes a live exit order. Run it against sandbox first, and confirm
// the symbol, prices and environment before pointing it at production.
//
// Nothing here is retried: a retried PATCH could move or duplicate an exit order.
// Quantity is not sent: the exit order is sized from the live position.
// The prices must suit the side of the position: the stop-loss trigger below
// LTP and the target above LTP for a long position, the reverse for a short.

using High.OpenApi;
using High.OpenApi.Models;

using var high = new HighClient();

try
{
    var stoploss = await high.Portfolio.SetStoplossAsync(new SetStoplossRequest
    {
        TradingSymbol = "RELIANCE-EQ",
        Flavor = Flavor.Regular,
        ProductType = ExitProductType.Intraday,
        TriggerPrice = 1350,
    });
    Console.WriteLine(
        $"{stoploss.Leg} {stoploss.Action}: orderId={stoploss.OrderId} " +
        $"trigger={stoploss.Price} limit={stoploss.LimitPrice} qty={stoploss.Quantity}");

    var target = await high.Portfolio.SetTargetAsync(new SetTargetRequest
    {
        TradingSymbol = "RELIANCE-EQ",
        Flavor = Flavor.Regular,
        ProductType = ExitProductType.Intraday,
        TargetPrice = 1450,
    });
    Console.WriteLine(
        $"{target.Leg} {target.Action}: orderId={target.OrderId} " +
        $"limit={target.Price} qty={target.Quantity}");
}
catch (HighApiError error)
{
    Console.Error.WriteLine($"[{error.Status}] {error.Code ?? "no code"}: {error.Message}");
    if (error.RequestId is not null) Console.Error.WriteLine($"requestId: {error.RequestId}");
    System.Environment.Exit(1);
}
