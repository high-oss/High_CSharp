// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using High.OpenApi.Http;
using High.OpenApi.Models;

namespace High.OpenApi.Resources;

public sealed class OrdersResource
{
    private readonly ResolvedConfig _config;

    internal OrdersResource(ResolvedConfig config) => _config = config;

    /// <summary>Places a regular, bracket, cover or GTT order. Never retried.</summary>
    public Task<OrderPlacementResult> Place(PlaceOrderRequest body, CancellationToken cancellationToken = default) =>
        HighHttpTransport.SendAsync<OrderPlacementResult>(_config,
            new RequestOptions { Method = HttpMethod.Post, Path = "/orders", Auth = AuthKind.Bearer, Body = body },
            cancellationToken);

    /// <summary>Modifies a pending order. Never retried.</summary>
    public Task<OrderPlacementResult> Modify(ModifyOrderRequest body, CancellationToken cancellationToken = default) =>
        HighHttpTransport.SendAsync<OrderPlacementResult>(_config,
            new RequestOptions { Method = HttpMethod.Patch, Path = "/orders", Auth = AuthKind.Bearer, Body = body },
            cancellationToken);

    /// <summary>One order, in the same shape as the order book.</summary>
    public Task<OrderBook> Get(string orderId, CancellationToken cancellationToken = default) =>
        HighHttpTransport.SendAsync<OrderBook>(_config,
            new RequestOptions
            {
                Method = HttpMethod.Get,
                Path = PathTemplate.Of("/orders/{orderId}", new Dictionary<string, string> { ["orderId"] = orderId }),
                Auth = AuthKind.Bearer,
            },
            cancellationToken);

    /// <summary>Cancels a pending order. Never retried.</summary>
    public Task<OrderCancelResult> Cancel(string orderId, CancellationToken cancellationToken = default) =>
        HighHttpTransport.SendAsync<OrderCancelResult>(_config,
            new RequestOptions
            {
                Method = HttpMethod.Delete,
                Path = PathTemplate.Of("/orders/{orderId}", new Dictionary<string, string> { ["orderId"] = orderId }),
                Auth = AuthKind.Bearer,
            },
            cancellationToken);

    /// <summary>Today's order book.</summary>
    public Task<OrderBook> List(CancellationToken cancellationToken = default) =>
        HighHttpTransport.SendAsync<OrderBook>(_config,
            new RequestOptions { Method = HttpMethod.Get, Path = "/orders/list", Auth = AuthKind.Bearer },
            cancellationToken);

    /// <summary>Today's trade book.</summary>
    public Task<TradeBook> Trades(CancellationToken cancellationToken = default) =>
        HighHttpTransport.SendAsync<TradeBook>(_config,
            new RequestOptions { Method = HttpMethod.Get, Path = "/orders/trades", Auth = AuthKind.Bearer },
            cancellationToken);

    /// <summary>The fills of one order.</summary>
    public Task<TradeBook> TradesFor(string orderId, CancellationToken cancellationToken = default) =>
        HighHttpTransport.SendAsync<TradeBook>(_config,
            new RequestOptions
            {
                Method = HttpMethod.Get,
                Path = PathTemplate.Of("/orders/{orderId}/trades", new Dictionary<string, string> { ["orderId"] = orderId }),
                Auth = AuthKind.Bearer,
            },
            cancellationToken);

    /// <summary>Estimated brokerage and statutory charges for one order.</summary>
    public Task<OrderCharges> Charges(OrderChargesRequest body, CancellationToken cancellationToken = default) =>
        HighHttpTransport.SendAsync<OrderCharges>(_config,
            new RequestOptions { Method = HttpMethod.Post, Path = "/orders/charges", Auth = AuthKind.Bearer, Body = body },
            cancellationToken);

    /// <summary>Margin required for a basket of orders.</summary>
    public Task<OrderMargin> Margin(IReadOnlyList<OrderMarginRequestLine> body, CancellationToken cancellationToken = default) =>
        HighHttpTransport.SendAsync<OrderMargin>(_config,
            new RequestOptions { Method = HttpMethod.Post, Path = "/orders/margin", Auth = AuthKind.Bearer, Body = body },
            cancellationToken);
}
