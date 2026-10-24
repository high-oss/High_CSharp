// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using System.Text.Json.Serialization;

namespace High.OpenApi.Models;

public sealed record DepthLevel
{
    [JsonPropertyName("price")] public required double Price { get; init; }
    [JsonPropertyName("quantity")] public required long Quantity { get; init; }
    [JsonPropertyName("orderCount")] public required long OrderCount { get; init; }
}

/// <summary>Five levels of bids and asks with aggregate pressure.</summary>
public sealed record MarketDepth
{
    [JsonPropertyName("tradingSymbol")] public required string TradingSymbol { get; init; }
    [JsonPropertyName("bids")] public required IReadOnlyList<DepthLevel> Bids { get; init; }
    [JsonPropertyName("asks")] public required IReadOnlyList<DepthLevel> Asks { get; init; }
    [JsonPropertyName("totalBidQuantity")] public required long TotalBidQuantity { get; init; }
    [JsonPropertyName("totalAskQuantity")] public required long TotalAskQuantity { get; init; }
    [JsonPropertyName("totalAskPercentage")] public required double TotalAskPercentage { get; init; }
    [JsonPropertyName("totalBidsPercentage")] public required double TotalBidsPercentage { get; init; }
}

public sealed record Expiry
{
    [JsonPropertyName("expiry")] public required string ExpiryDate { get; init; }

    /// <summary>M = monthly, W = weekly.</summary>
    [JsonPropertyName("type")] public required ExpiryFrequency Type { get; init; }
}

/// <summary>
/// Columnar candles: every array has the same length, and index i across the
/// arrays describes one candle.
/// </summary>
public sealed record HistoricalCandles
{
    /// <summary>Scrip key of the series.</summary>
    [JsonPropertyName("tradingSymbol")] public required string TradingSymbol { get; init; }

    [JsonPropertyName("interval")] public required string Interval { get; init; }

    /// <summary>Unix seconds, one per candle.</summary>
    [JsonPropertyName("timestamp")] public required IReadOnlyList<long> Timestamp { get; init; }

    [JsonPropertyName("open")] public required IReadOnlyList<double> Open { get; init; }
    [JsonPropertyName("high")] public required IReadOnlyList<double> High { get; init; }
    [JsonPropertyName("low")] public required IReadOnlyList<double> Low { get; init; }
    [JsonPropertyName("close")] public required IReadOnlyList<double> Close { get; init; }
    [JsonPropertyName("volume")] public required IReadOnlyList<long> Volume { get; init; }

    /// <summary>Open interest per candle. Derivatives only.</summary>
    [JsonPropertyName("oi")] public IReadOnlyList<long>? Oi { get; init; }
}

/// <summary>
/// One side (call or put) of an option-chain row. A strike with no contract on
/// this side is an empty object — every property here is optional — and
/// <see cref="Touchline"/> is absent when the quote feed has no entry for the strike.
/// </summary>
public sealed record OptionLeg
{
    [JsonPropertyName("tradingSymbol")] public string? TradingSymbol { get; init; }
    [JsonPropertyName("scripKey")] public string? ScripKey { get; init; }
    [JsonPropertyName("token")] public long? Token { get; init; }
    [JsonPropertyName("touchline")] public Touchline? Touchline { get; init; }
}

public sealed record OptionChainRow
{
    [JsonPropertyName("strikePrice")] public required double StrikePrice { get; init; }

    /// <summary>Present only on the at-the-money row.</summary>
    [JsonPropertyName("isATM")] public bool? IsAtm { get; init; }

    [JsonPropertyName("call")] public required OptionLeg Call { get; init; }
    [JsonPropertyName("put")] public required OptionLeg Put { get; init; }
}

public sealed record OptionChain
{
    [JsonPropertyName("baseStockInfo")] public required ScripInfo BaseStockInfo { get; init; }
    [JsonPropertyName("optionChain")] public required IReadOnlyList<OptionChainRow> Chain { get; init; }
    [JsonPropertyName("marketLot")] public required long MarketLot { get; init; }

    /// <summary>Exchange freeze limit in lots.</summary>
    [JsonPropertyName("maxOrderLots")] public required long MaxOrderLots { get; init; }
}

// ---- Request bodies -------------------------------------------------------

/// <summary>Body of <c>POST /scrips/quotes</c> (<c>scrips.Quotes</c>). Send <see cref="Symbols"/>, <see cref="ScripKeys"/> or both; at least one must match an instrument.</summary>
public sealed record QuotesRequest
{
    [JsonPropertyName("symbols")] public IReadOnlyList<string>? Symbols { get; init; }
    [JsonPropertyName("scripKeys")] public IReadOnlyList<string>? ScripKeys { get; init; }
}

/// <summary>Body of <c>POST /scrips/ohlc</c> (<c>scrips.Ohlc</c>).</summary>
public sealed record OhlcRequest
{
    [JsonPropertyName("symbols")] public IReadOnlyList<string>? Symbols { get; init; }
    [JsonPropertyName("scripKeys")] public IReadOnlyList<string>? ScripKeys { get; init; }
}

/// <summary>
/// Body of <c>POST /scrips/historical</c> (<c>scrips.Historical</c>). Name the
/// instrument by <see cref="TradingSymbol"/>, or by <see cref="ExchangeSegment"/>
/// together with <see cref="ScripCode"/>.
/// </summary>
public sealed record HistoricalRequest
{
    /// <summary>Trading symbol from the instrument list. Leave it null when naming the instrument by segment and code.</summary>
    [JsonPropertyName("tradingSymbol")] public string? TradingSymbol { get; init; }

    /// <summary>Exchange segment. Send it with <see cref="ScripCode"/> when <see cref="TradingSymbol"/> is null.</summary>
    [JsonPropertyName("exchangeSegment")] public ExchangeSegment? ExchangeSegment { get; init; }

    /// <summary>Broker scrip code (token). Send it with <see cref="ExchangeSegment"/> when <see cref="TradingSymbol"/> is null.</summary>
    [JsonPropertyName("scripCode")] public string? ScripCode { get; init; }

    [JsonPropertyName("interval")] public required HistoricalInterval Interval { get; init; }

    /// <summary>Start of the range, Unix epoch seconds.</summary>
    [JsonPropertyName("fromTime")] public required long FromTime { get; init; }

    /// <summary>End of the range, Unix epoch seconds. Candles that open after it are left out.</summary>
    [JsonPropertyName("toTime")] public required long ToTime { get; init; }

    /// <summary>Accepted but not used: every series is always returned.</summary>
    [JsonPropertyName("fields")] public IReadOnlyList<string>? Fields { get; init; }
}

/// <summary>
/// Body of <c>POST /scrips/intraday</c> (<c>scrips.Intraday</c>). Name the
/// instrument by <see cref="TradingSymbol"/>, or by <see cref="ExchangeSegment"/>
/// together with <see cref="ScripCode"/>.
/// </summary>
public sealed record IntradayRequest
{
    /// <summary>Trading symbol from the instrument list. Leave it null when naming the instrument by segment and code.</summary>
    [JsonPropertyName("tradingSymbol")] public string? TradingSymbol { get; init; }

    /// <summary>Exchange segment. Send it with <see cref="ScripCode"/> when <see cref="TradingSymbol"/> is null.</summary>
    [JsonPropertyName("exchangeSegment")] public ExchangeSegment? ExchangeSegment { get; init; }

    /// <summary>Broker scrip code (token). Send it with <see cref="ExchangeSegment"/> when <see cref="TradingSymbol"/> is null.</summary>
    [JsonPropertyName("scripCode")] public string? ScripCode { get; init; }

    /// <summary>Candle length. The intraday window is the last trading session, so there is no day-or-longer interval.</summary>
    [JsonPropertyName("interval")] public required IntradayInterval Interval { get; init; }

    /// <summary>Accepted but not used: every series is always returned.</summary>
    [JsonPropertyName("fields")] public IReadOnlyList<string>? Fields { get; init; }
}

/// <summary>Body of <c>POST /scrips/option-chain</c> (<c>scrips.OptionChain</c>).</summary>
public sealed record OptionChainRequest
{
    /// <summary>The underlying: a stock's trading symbol, an index, or any F&amp;O contract on it.</summary>
    [JsonPropertyName("tradingSymbol")] public required string TradingSymbol { get; init; }

    /// <summary>Expiry date, YYYY-MM-DD. Leave it null for the nearest expiry.</summary>
    [JsonPropertyName("expiry")] public string? Expiry { get; init; }

    /// <summary>Strikes to return on each side of the at-the-money strike. Leave it null for the full chain.</summary>
    [JsonPropertyName("strikeCount")] public double? StrikeCount { get; init; }
}
