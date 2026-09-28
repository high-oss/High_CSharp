// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

// Places a limit order and reads it back. Requires a registered static IP.
//
//   HIGH_ENVIRONMENT=sandbox HIGH_ACCESS_TOKEN=... dotnet run --project examples/PlaceOrder
//
// Nothing here is retried: a retried order placement would be a duplicate order.

using High.OpenApi;
using High.OpenApi.Models;

using var high = new HighClient();

try
{
    var charges = await high.Orders.Charges(new OrderChargesRequest
    {
        TradingSymbol = "RELIANCE-EQ",
        Quantity = 1,
        Price = 1400,
        ProductType = ProductType.Delivery,
        TradeSide = TradeSide.Buy,
    });
    Console.WriteLine($"Estimated charges: {charges.Charges.TotalBuyCharges}");

    var placed = await high.Orders.Place(new PlaceOrderRequest
    {
        TradeSide = TradeSide.Buy,
        TradingSymbol = "RELIANCE-EQ",
        ProductType = ProductType.Delivery,
        Flavor = Flavor.Regular,
        OrderType = OrderTypeEnum.Limit,
        Validity = Validity.Day,
        Quantity = 1,
        Price = 1400,
        IsAmo = false,
    });

    // OrderPlacementResult.Error is required and empty on success — an order
    // can be accepted by the API and still rejected by the exchange.
    if (!string.IsNullOrEmpty(placed.Error))
    {
        Console.Error.WriteLine($"Order rejected: {placed.Error}");
        System.Environment.Exit(1);
        return;
    }

    Console.WriteLine($"Order id: {placed.OrderId}");

    var book = await high.Orders.Get(placed.OrderId);
    Console.WriteLine($"Status: {book.Orders.FirstOrDefault()?.OrderStatus}");
}
catch (HighApiError error)
{
    Console.Error.WriteLine($"[{error.Status}] {error.Code ?? "no code"}: {error.Message}");
    if (error.RequestId is not null) Console.Error.WriteLine($"requestId: {error.RequestId}");
    System.Environment.Exit(1);
}
