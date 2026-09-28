// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace High.OpenApi.Models;

/// <summary>One order from the order book.</summary>
public sealed record Order
{
    [JsonPropertyName("orderId")] public required string OrderId { get; init; }
    [JsonPropertyName("exchangeOrderId")] public string? ExchangeOrderId { get; init; }
    [JsonPropertyName("clientId")] public string? ClientId { get; init; }
    [JsonPropertyName("tradeSide")] public required TradeSide TradeSide { get; init; }

    /// <summary>
    /// Exchange segment as returned by the broker. Wider than the tradeable
    /// set: a book shared with other Choice channels can carry NSECDS, BSECDS
    /// or MCX, so this is a plain string rather than <see cref="Models.ExchangeSegment"/>.
    /// </summary>
    [JsonPropertyName("exchangeSegment")] public required string ExchangeSegment { get; init; }

    /// <summary>Broker token as a string. Note: an integer on <see cref="ScripInfo"/>.</summary>
    [JsonPropertyName("scripCode")] public required string ScripCode { get; init; }

    [JsonPropertyName("flavor")] public required Flavor Flavor { get; init; }
    [JsonPropertyName("orderType")] public required OrderTypeEnum OrderType { get; init; }

    /// <summary>Plain string: the broker can return values outside DAY/IOC/YEAR.</summary>
    [JsonPropertyName("validity")] public required string Validity { get; init; }

    [JsonPropertyName("productType")] public required ProductType ProductType { get; init; }
    [JsonPropertyName("isAMO")] public required bool IsAmo { get; init; }
    [JsonPropertyName("quantity")] public required long Quantity { get; init; }
    [JsonPropertyName("tradedQty")] public required long TradedQty { get; init; }
    [JsonPropertyName("disclosedQuantity")] public required long DisclosedQuantity { get; init; }
    [JsonPropertyName("price")] public required double Price { get; init; }
    [JsonPropertyName("triggerPrice")] public required double TriggerPrice { get; init; }
    [JsonPropertyName("targetPrice")] public required double TargetPrice { get; init; }
    [JsonPropertyName("stoplossPrice")] public required double StoplossPrice { get; init; }
    [JsonPropertyName("orderStatus")] public required string OrderStatus { get; init; }

    /// <summary>Rejection reason. Null for Traded and Transit orders, and absent when the broker returned none.</summary>
    [JsonPropertyName("error")] public string? Error { get; init; }

    /// <summary>Present once any quantity has traded.</summary>
    [JsonPropertyName("avgTradedPrice")] public double? AvgTradedPrice { get; init; }

    [JsonPropertyName("createdAt")] public required string CreatedAt { get; init; }
    [JsonPropertyName("updatedAt")] public required string UpdatedAt { get; init; }

    /// <summary>Absent when the instrument is not in the local master.</summary>
    [JsonPropertyName("tradingSymbol")] public string? TradingSymbol { get; init; }

    /// <summary>Broker-supplied order detail block. Usually null.</summary>
    [JsonPropertyName("details")] public JsonNode? Details { get; init; }

    /// <summary>Which leg of a bracket or cover order this is. Absent on regular orders.</summary>
    [JsonPropertyName("legIndicator")] public string? LegIndicator { get; init; }

    /// <summary>Order id of the entry leg, for bracket and cover legs.</summary>
    [JsonPropertyName("mainLegOrderId")] public string? MainLegOrderId { get; init; }
}

/// <summary>One fill against an order.</summary>
public sealed record Trade
{
    [JsonPropertyName("tradeId")] public required string TradeId { get; init; }
    [JsonPropertyName("orderId")] public required string OrderId { get; init; }
    [JsonPropertyName("tradeSide")] public required TradeSide TradeSide { get; init; }

    /// <summary>Plain string, unlike <see cref="Order.ExchangeSegment"/>'s equally plain — but here the spec gives no enum either way.</summary>
    [JsonPropertyName("exchangeSegment")] public required string ExchangeSegment { get; init; }

    /// <summary>Broker token as a string.</summary>
    [JsonPropertyName("scripCode")] public required string ScripCode { get; init; }

    [JsonPropertyName("symbol")] public required string Symbol { get; init; }
    [JsonPropertyName("quantity")] public required long Quantity { get; init; }
    [JsonPropertyName("avgTradedPrice")] public required double AvgTradedPrice { get; init; }

    /// <summary>Plain string: the spec gives this field no enum on <c>Trade</c>, unlike <c>Order.productType</c>.</summary>
    [JsonPropertyName("productType")] public required string ProductType { get; init; }

    [JsonPropertyName("createdAt")] public required string CreatedAt { get; init; }
    [JsonPropertyName("tradingSymbol")] public string? TradingSymbol { get; init; }
}

public sealed record OrderBook
{
    [JsonPropertyName("orders")] public required IReadOnlyList<Order> Orders { get; init; }
    [JsonPropertyName("scrips")] public required Dictionary<string, ScripInfo> Scrips { get; init; }
}

public sealed record TradeBook
{
    [JsonPropertyName("trades")] public required IReadOnlyList<Trade> Trades { get; init; }
    [JsonPropertyName("scrips")] public required Dictionary<string, ScripInfo> Scrips { get; init; }
}

/// <summary>Result of placing or modifying an order. A large order may be sliced into several exchange orders.</summary>
public sealed record OrderPlacementResult
{
    [JsonPropertyName("orderId")] public required string OrderId { get; init; }

    /// <summary>Empty when the order was accepted.</summary>
    [JsonPropertyName("error")] public required string Error { get; init; }

    [JsonPropertyName("isSliced")] public bool? IsSliced { get; init; }

    /// <summary>Every order id created, including slices.</summary>
    [JsonPropertyName("orderIds")] public IReadOnlyList<string>? OrderIds { get; init; }
}

public sealed record OrderCancelResult
{
    [JsonPropertyName("orderId")] public required string OrderId { get; init; }
    [JsonPropertyName("error")] public required string Error { get; init; }
}

/// <summary>Estimated statutory and broker charges for one order.</summary>
public sealed record OrderCharges
{
    [JsonPropertyName("quantity")] public required long Quantity { get; init; }
    [JsonPropertyName("price")] public required double Price { get; init; }

    /// <summary>Plain string: the spec gives this field no enum here, unlike the request's <see cref="ProductType"/>.</summary>
    [JsonPropertyName("productType")] public required string ProductType { get; init; }

    [JsonPropertyName("tradeSide")] public required TradeSide TradeSide { get; init; }
    [JsonPropertyName("tradedValue")] public required double TradedValue { get; init; }
    [JsonPropertyName("charges")] public required OrderChargesBreakdown Charges { get; init; }
}

public sealed record OrderChargesBreakdown
{
    [JsonPropertyName("brokerage")] public required double Brokerage { get; init; }
    [JsonPropertyName("stt")] public required double Stt { get; init; }
    [JsonPropertyName("clearingCharges")] public required double ClearingCharges { get; init; }
    [JsonPropertyName("dpCharges")] public required double DpCharges { get; init; }
    [JsonPropertyName("stampDuty")] public required double StampDuty { get; init; }
    [JsonPropertyName("transactionCharges")] public required double TransactionCharges { get; init; }
    [JsonPropertyName("turnoverCharges")] public required double TurnoverCharges { get; init; }
    [JsonPropertyName("gst")] public required double Gst { get; init; }
    [JsonPropertyName("totalBuyCharges")] public required double TotalBuyCharges { get; init; }
    [JsonPropertyName("totalSellCharges")] public required double TotalSellCharges { get; init; }
    [JsonPropertyName("breakevenPrice")] public required double BreakevenPrice { get; init; }
}

/// <summary>Margin required for a basket of orders, with the SPAN summary across them.</summary>
public sealed record OrderMargin
{
    [JsonPropertyName("margins")] public required IReadOnlyList<OrderMarginLine> Margins { get; init; }
    [JsonPropertyName("spanSummary")] public required SpanSummary SpanSummary { get; init; }
}

public sealed record OrderMarginLine
{
    [JsonPropertyName("tradingSymbol")] public required string TradingSymbol { get; init; }
    [JsonPropertyName("scripKey")] public required string ScripKey { get; init; }
    [JsonPropertyName("quantity")] public required long Quantity { get; init; }
    [JsonPropertyName("tradeSide")] public required TradeSide TradeSide { get; init; }
    [JsonPropertyName("initialMargin")] public required double InitialMargin { get; init; }
    [JsonPropertyName("exposureMargin")] public required double ExposureMargin { get; init; }
}

public sealed record SpanSummary
{
    [JsonPropertyName("span")] public required double Span { get; init; }
    [JsonPropertyName("exposureMargin")] public required double ExposureMargin { get; init; }
    [JsonPropertyName("optionPremium")] public required double OptionPremium { get; init; }
    [JsonPropertyName("marginBenefit")] public required double MarginBenefit { get; init; }
    [JsonPropertyName("totalMargin")] public required double TotalMargin { get; init; }
}

// ---- Request bodies -------------------------------------------------------

/// <summary>Body of <c>POST /orders</c> (<c>orders.Place</c>).</summary>
public sealed record PlaceOrderRequest
{
    [JsonPropertyName("tradeSide")] public required TradeSide TradeSide { get; init; }

    /// <summary>Required unless <see cref="ExchangeSegment"/> + <see cref="ScripCode"/> are sent.</summary>
    [JsonPropertyName("tradingSymbol")] public string? TradingSymbol { get; init; }

    /// <summary>Required when <see cref="TradingSymbol"/> is not sent.</summary>
    [JsonPropertyName("exchangeSegment")] public ExchangeSegment? ExchangeSegment { get; init; }

    /// <summary>Broker scrip code (token). Required when <see cref="TradingSymbol"/> is not sent.</summary>
    [JsonPropertyName("scripCode")] public string? ScripCode { get; init; }

    [JsonPropertyName("productType")] public required ProductType ProductType { get; init; }
    [JsonPropertyName("flavor")] public required Flavor Flavor { get; init; }

    /// <summary>BO allows LIMIT only; CO and GTT allow MARKET or LIMIT.</summary>
    [JsonPropertyName("orderType")] public required OrderTypeEnum OrderType { get; init; }

    [JsonPropertyName("validity")] public required Validity Validity { get; init; }

    /// <summary>Quantity in units (a multiple of lot size for derivatives).</summary>
    [JsonPropertyName("quantity")] public required long Quantity { get; init; }

    /// <summary>Disclosed quantity (REGULAR DAY orders only).</summary>
    [JsonPropertyName("disclosedQuantity")] public long? DisclosedQuantity { get; init; }

    /// <summary>Limit price in Rupees. Required for LIMIT and STOP_LOSS.</summary>
    [JsonPropertyName("price")] public double? Price { get; init; }

    /// <summary>Trigger price in Rupees. Required for STOP_LOSS, STOP_LOSS_MARKET and GTT.</summary>
    [JsonPropertyName("triggerPrice")] public double? TriggerPrice { get; init; }

    [JsonPropertyName("isAMO")] public required bool IsAmo { get; init; }

    /// <summary>Target price in Rupees (BO).</summary>
    [JsonPropertyName("targetPrice")] public double? TargetPrice { get; init; }

    /// <summary>Stop-loss price in Rupees (BO/CO).</summary>
    [JsonPropertyName("stoplossPrice")] public double? StoplossPrice { get; init; }

    /// <summary>Trailing stop-loss in ticks.</summary>
    [JsonPropertyName("trailSL")] public double? TrailSl { get; init; }

    /// <summary>Slice into exchange-size child orders.</summary>
    [JsonPropertyName("isSlice")] public bool? IsSlice { get; init; }
}

/// <summary>Body of <c>PATCH /orders</c> (<c>orders.Modify</c>).</summary>
public sealed record ModifyOrderRequest
{
    /// <summary>Broker order number. Required when modifying.</summary>
    [JsonPropertyName("orderId")] public required string OrderId { get; init; }

    [JsonPropertyName("tradeSide")] public required TradeSide TradeSide { get; init; }

    /// <summary>Required unless <see cref="ExchangeSegment"/> + <see cref="ScripCode"/> are sent.</summary>
    [JsonPropertyName("tradingSymbol")] public string? TradingSymbol { get; init; }

    [JsonPropertyName("exchangeSegment")] public ExchangeSegment? ExchangeSegment { get; init; }
    [JsonPropertyName("scripCode")] public string? ScripCode { get; init; }
    [JsonPropertyName("productType")] public required ProductType ProductType { get; init; }
    [JsonPropertyName("flavor")] public required Flavor Flavor { get; init; }
    [JsonPropertyName("orderType")] public required OrderTypeEnum OrderType { get; init; }
    [JsonPropertyName("validity")] public required Validity Validity { get; init; }
    [JsonPropertyName("quantity")] public required long Quantity { get; init; }
    [JsonPropertyName("disclosedQuantity")] public long? DisclosedQuantity { get; init; }
    [JsonPropertyName("price")] public double? Price { get; init; }
    [JsonPropertyName("triggerPrice")] public double? TriggerPrice { get; init; }
    [JsonPropertyName("isAMO")] public required bool IsAmo { get; init; }
    [JsonPropertyName("targetPrice")] public double? TargetPrice { get; init; }
    [JsonPropertyName("stoplossPrice")] public double? StoplossPrice { get; init; }
    [JsonPropertyName("trailSL")] public double? TrailSl { get; init; }

    /// <summary>Leg to modify (BO/CO only).</summary>
    [JsonPropertyName("legIndicator")] public LegIndicator? LegIndicator { get; init; }

    [JsonPropertyName("isSlice")] public bool? IsSlice { get; init; }
}

/// <summary>Body of <c>POST /orders/charges</c> (<c>orders.Charges</c>).</summary>
public sealed record OrderChargesRequest
{
    [JsonPropertyName("tradingSymbol")] public string? TradingSymbol { get; init; }
    [JsonPropertyName("exchangeSegment")] public ExchangeSegment? ExchangeSegment { get; init; }
    [JsonPropertyName("scripCode")] public string? ScripCode { get; init; }
    [JsonPropertyName("quantity")] public required long Quantity { get; init; }
    [JsonPropertyName("tradeSide")] public required TradeSide TradeSide { get; init; }
    [JsonPropertyName("productType")] public required ProductType ProductType { get; init; }

    /// <summary>Price in Rupees to estimate at. Leave it null to use the LTP.</summary>
    [JsonPropertyName("price")] public double? Price { get; init; }
}

/// <summary>One line of the body of <c>POST /orders/margin</c> (<c>orders.Margin</c>), which takes an array.</summary>
public sealed record OrderMarginRequestLine
{
    [JsonPropertyName("tradingSymbol")] public string? TradingSymbol { get; init; }
    [JsonPropertyName("exchangeSegment")] public ExchangeSegment? ExchangeSegment { get; init; }
    [JsonPropertyName("scripCode")] public string? ScripCode { get; init; }
    [JsonPropertyName("quantity")] public required long Quantity { get; init; }
    [JsonPropertyName("tradeSide")] public required TradeSide TradeSide { get; init; }
    [JsonPropertyName("productType")] public required ProductType ProductType { get; init; }
    [JsonPropertyName("price")] public double? Price { get; init; }
}
