// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using System.Text.Json.Serialization;

namespace High.OpenApi.Models;

/// <summary>Booked and unrealised profit and loss. Used by <see cref="Positions"/>.</summary>
public sealed record PnlSnapshot
{
    [JsonPropertyName("bookedPL")] public required double BookedPl { get; init; }
    [JsonPropertyName("unrealisedPL")] public required double UnrealisedPl { get; init; }
    [JsonPropertyName("totalPL")] public required double TotalPl { get; init; }
    [JsonPropertyName("bookedPLPercent")] public required double BookedPlPercent { get; init; }
    [JsonPropertyName("unrealisedPLPercent")] public required double UnrealisedPlPercent { get; init; }
    [JsonPropertyName("totalPLPercent")] public required double TotalPlPercent { get; init; }
}

/// <summary>Invested and current value with day and total change. Used by <see cref="Holdings"/>.</summary>
public sealed record InvestmentSnapshot
{
    [JsonPropertyName("investment")] public required double Investment { get; init; }
    [JsonPropertyName("currentValue")] public required double CurrentValue { get; init; }
    [JsonPropertyName("previousDayValue")] public required double PreviousDayValue { get; init; }
    [JsonPropertyName("totalPL")] public required double TotalPl { get; init; }
    [JsonPropertyName("dayPL")] public required double DayPl { get; init; }
    [JsonPropertyName("totalPLPercent")] public required double TotalPlPercent { get; init; }
    [JsonPropertyName("dayPLPercent")] public required double DayPlPercent { get; init; }
}

public sealed record Position
{
    [JsonPropertyName("netQty")] public required long NetQty { get; init; }
    [JsonPropertyName("buyQty")] public required long BuyQty { get; init; }
    [JsonPropertyName("sellQty")] public required long SellQty { get; init; }
    [JsonPropertyName("isMTF")] public required bool IsMtf { get; init; }
    [JsonPropertyName("buyAvgPrice")] public required double BuyAvgPrice { get; init; }
    [JsonPropertyName("sellAvgPrice")] public required double SellAvgPrice { get; init; }
    [JsonPropertyName("exchangeSegment")] public required string ExchangeSegment { get; init; }
    [JsonPropertyName("scripCode")] public required string ScripCode { get; init; }
    [JsonPropertyName("tradingSymbol")] public required string TradingSymbol { get; init; }
    [JsonPropertyName("snapshot")] public required PnlSnapshot Snapshot { get; init; }
    [JsonPropertyName("productType")] public required ProductType ProductType { get; init; }
    [JsonPropertyName("flavor")] public required Flavor Flavor { get; init; }
}

/// <summary>Open intraday and carry-forward positions with their P&amp;L snapshot.</summary>
public sealed record Positions
{
    [JsonPropertyName("totalStocks")] public required long TotalStocks { get; init; }
    [JsonPropertyName("snapshot")] public required PnlSnapshot Snapshot { get; init; }
    [JsonPropertyName("positions")] public required IReadOnlyList<Position> PositionList { get; init; }
    [JsonPropertyName("scrips")] public required Dictionary<string, ScripInfo> Scrips { get; init; }
}

public sealed record Holding
{
    [JsonPropertyName("netQty")] public required long NetQty { get; init; }
    [JsonPropertyName("buyQty")] public required long BuyQty { get; init; }
    [JsonPropertyName("sellQty")] public required long SellQty { get; init; }
    [JsonPropertyName("isMTF")] public required bool IsMtf { get; init; }
    [JsonPropertyName("buyAvgPrice")] public required double BuyAvgPrice { get; init; }
    [JsonPropertyName("sellAvgPrice")] public required double SellAvgPrice { get; init; }
    [JsonPropertyName("exchangeSegment")] public required string ExchangeSegment { get; init; }
    [JsonPropertyName("scripCode")] public required string ScripCode { get; init; }
    [JsonPropertyName("tradingSymbol")] public required string TradingSymbol { get; init; }
    [JsonPropertyName("snapshot")] public required InvestmentSnapshot Snapshot { get; init; }
}

/// <summary>Demat holdings with their investment snapshot.</summary>
public sealed record Holdings
{
    [JsonPropertyName("totalStocks")] public required long TotalStocks { get; init; }
    [JsonPropertyName("snapshot")] public required InvestmentSnapshot Snapshot { get; init; }
    [JsonPropertyName("holdings")] public required IReadOnlyList<Holding> HoldingList { get; init; }
    [JsonPropertyName("scrips")] public required Dictionary<string, ScripInfo> Scrips { get; init; }
}

/// <summary>Cash, margin and charge balances for the trading account.</summary>
public sealed record Funds
{
    [JsonPropertyName("availableBalance")] public required double AvailableBalance { get; init; }
    [JsonPropertyName("ledgerBalance")] public required double LedgerBalance { get; init; }
    [JsonPropertyName("todaysBalance")] public required double TodaysBalance { get; init; }
    [JsonPropertyName("marginUtilized")] public required double MarginUtilized { get; init; }
    [JsonPropertyName("marginAgainstAssets")] public required double MarginAgainstAssets { get; init; }
    [JsonPropertyName("todaysPayIn")] public required double TodaysPayIn { get; init; }
    [JsonPropertyName("todaysPayout")] public required double TodaysPayout { get; init; }
    [JsonPropertyName("mtfFunds")] public required MtfFunds MtfFunds { get; init; }
    [JsonPropertyName("charges")] public required FundsCharges Charges { get; init; }
    [JsonPropertyName("unsettledFutureAmount")] public required double UnsettledFutureAmount { get; init; }
}

public sealed record MtfFunds
{
    [JsonPropertyName("mtfCash")] public required double MtfCash { get; init; }
    [JsonPropertyName("mtfFunded")] public required double MtfFunded { get; init; }
    [JsonPropertyName("totalMTFFunding")] public required double TotalMtfFunding { get; init; }
}

public sealed record FundsCharges
{
    [JsonPropertyName("delayedPaymentCharges")] public required double DelayedPaymentCharges { get; init; }
    [JsonPropertyName("dpCharges")] public required double DpCharges { get; init; }
    [JsonPropertyName("totalCharges")] public required double TotalCharges { get; init; }
}

public sealed record PositionConvertResult
{
    [JsonPropertyName("tradingSymbol")] public required string TradingSymbol { get; init; }
    [JsonPropertyName("exchangeSegment")] public required string ExchangeSegment { get; init; }
    [JsonPropertyName("scripCode")] public required string ScripCode { get; init; }
    [JsonPropertyName("quantity")] public required long Quantity { get; init; }
    [JsonPropertyName("isSubmitted")] public required bool IsSubmitted { get; init; }

    /// <summary>Margin shortfall blocking the conversion, if any.</summary>
    [JsonPropertyName("shortfall")] public double? Shortfall { get; init; }
}

/// <summary>
/// One square-off attempt. <see cref="Error"/> is present only on failure, and
/// <see cref="TradingSymbol"/> may be empty for derivatives the broker did not name.
/// </summary>
public sealed record PositionExitResult
{
    [JsonPropertyName("tradingSymbol")] public required string TradingSymbol { get; init; }
    [JsonPropertyName("exchangeSegment")] public required string ExchangeSegment { get; init; }
    [JsonPropertyName("scripCode")] public required string ScripCode { get; init; }
    [JsonPropertyName("quantity")] public required long Quantity { get; init; }
    [JsonPropertyName("flavor")] public required Flavor Flavor { get; init; }
    [JsonPropertyName("isSubmitted")] public required bool IsSubmitted { get; init; }

    /// <summary>Present only when the square-off failed.</summary>
    [JsonPropertyName("error")] public string? Error { get; init; }
}

public sealed record PositionExitAllResult
{
    [JsonPropertyName("totalCount")] public required long TotalCount { get; init; }
    [JsonPropertyName("successCount")] public required long SuccessCount { get; init; }
    [JsonPropertyName("failureCount")] public required long FailureCount { get; init; }
    [JsonPropertyName("positions")] public required IReadOnlyList<PositionExitResult> Positions { get; init; }
}

// ---- Request bodies -------------------------------------------------------

/// <summary>Body of <c>PATCH /portfolio/positions/convert</c> (<c>portfolio.ConvertPosition</c>).</summary>
public sealed record ConvertPositionRequest
{
    [JsonPropertyName("tradingSymbol")] public string? TradingSymbol { get; init; }
    [JsonPropertyName("exchangeSegment")] public ExchangeSegment? ExchangeSegment { get; init; }
    [JsonPropertyName("scripCode")] public string? ScripCode { get; init; }
    [JsonPropertyName("tradeSide")] public required TradeSide TradeSide { get; init; }

    /// <summary>Quantity to convert.</summary>
    [JsonPropertyName("quantity")] public required long Quantity { get; init; }

    [JsonPropertyName("sourceProductType")] public required ProductType SourceProductType { get; init; }
    [JsonPropertyName("targetProductType")] public required ProductType TargetProductType { get; init; }

    /// <summary>Order number, blank for position-level conversion.</summary>
    [JsonPropertyName("clientOrderNo")] public string? ClientOrderNo { get; init; }
}

/// <summary>Body of <c>DELETE /portfolio/positions/exit</c> (<c>portfolio.ExitPosition</c>). The API expects a body on this DELETE.</summary>
public sealed record ExitPositionRequest
{
    [JsonPropertyName("tradingSymbol")] public string? TradingSymbol { get; init; }
    [JsonPropertyName("exchangeSegment")] public ExchangeSegment? ExchangeSegment { get; init; }
    [JsonPropertyName("scripCode")] public string? ScripCode { get; init; }

    /// <summary>Side of the exit order.</summary>
    [JsonPropertyName("tradeSide")] public required TradeSide TradeSide { get; init; }

    /// <summary>Product of the position. Only INTRADAY/DELIVERY — no MTF.</summary>
    [JsonPropertyName("productType")] public required ExitProductType ProductType { get; init; }

    /// <summary>Quantity to square off.</summary>
    [JsonPropertyName("quantity")] public required long Quantity { get; init; }

    /// <summary>Flavor of the original position. Only REGULAR/BO/CO — no GTT.</summary>
    [JsonPropertyName("flavor")] public required ExitFlavor Flavor { get; init; }
}
