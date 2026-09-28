// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using System.Text.Json.Serialization;

namespace High.OpenApi.Models;

/// <summary>Open, high, low, close and volume for the current session.</summary>
public sealed record Ohlcv
{
    [JsonPropertyName("open")] public required double Open { get; init; }
    [JsonPropertyName("high")] public required double High { get; init; }
    [JsonPropertyName("low")] public required double Low { get; init; }
    [JsonPropertyName("close")] public required double Close { get; init; }
    [JsonPropertyName("volume")] public required long Volume { get; init; }

    /// <summary>Open interest. Derivatives only.</summary>
    [JsonPropertyName("oi")] public long? Oi { get; init; }
}

/// <summary>
/// Last traded price and session change for one scrip. Open-interest fields
/// are present for derivatives only.
/// </summary>
public sealed record Touchline
{
    /// <summary>Scrip key, <c>&lt;segment&gt;@&lt;token&gt;</c>.</summary>
    [JsonPropertyName("scrip")] public required string Scrip { get; init; }

    /// <summary>Last traded price.</summary>
    [JsonPropertyName("LTP")] public required double Ltp { get; init; }

    [JsonPropertyName("prevClose")] public required double PrevClose { get; init; }
    [JsonPropertyName("change")] public required double Change { get; init; }
    [JsonPropertyName("changePer")] public required double ChangePer { get; init; }

    /// <summary>Open interest. Derivatives only.</summary>
    [JsonPropertyName("oi")] public long? Oi { get; init; }

    /// <summary>Derivatives only.</summary>
    [JsonPropertyName("prevOI")] public long? PrevOi { get; init; }

    /// <summary>Derivatives only.</summary>
    [JsonPropertyName("oiChange")] public long? OiChange { get; init; }

    /// <summary>Derivatives only.</summary>
    [JsonPropertyName("oiChangePer")] public double? OiChangePer { get; init; }

    /// <summary>Present on an OHLC request; absent from a plain quote.</summary>
    [JsonPropertyName("ohlcv")] public Ohlcv? Ohlcv { get; init; }
}

/// <summary>
/// Reference data for one tradable instrument. Equity-only fields (isin,
/// sector, industry) and derivative-only fields (underlyingSymbol, expiry,
/// strikePrice) are both optional.
/// </summary>
public sealed record ScripInfo
{
    [JsonPropertyName("tradingSymbol")] public required string TradingSymbol { get; init; }

    /// <summary>Broker token. Note: a <c>string</c> on <see cref="Order"/> and <see cref="Trade"/>.</summary>
    [JsonPropertyName("scripCode")] public required long ScripCode { get; init; }

    [JsonPropertyName("exchange")] public required string Exchange { get; init; }

    /// <summary>ES = equity spot, FO = futures and options.</summary>
    [JsonPropertyName("segment")] public required string Segment { get; init; }

    [JsonPropertyName("scripKey")] public required string ScripKey { get; init; }
    [JsonPropertyName("logo")] public string? Logo { get; init; }
    [JsonPropertyName("symbol")] public required string Symbol { get; init; }

    /// <summary>Derivatives only.</summary>
    [JsonPropertyName("underlyingSymbol")] public string? UnderlyingSymbol { get; init; }

    [JsonPropertyName("scripName")] public required string ScripName { get; init; }
    [JsonPropertyName("marketLot")] public required long MarketLot { get; init; }
    [JsonPropertyName("priceTick")] public required double PriceTick { get; init; }

    /// <summary>Equity only.</summary>
    [JsonPropertyName("isin")] public string? Isin { get; init; }

    /// <summary>Equity only.</summary>
    [JsonPropertyName("sector")] public string? Sector { get; init; }

    /// <summary>Equity only.</summary>
    [JsonPropertyName("industry")] public string? Industry { get; init; }

    /// <summary>Derivatives only.</summary>
    [JsonPropertyName("expiry")] public string? Expiry { get; init; }

    /// <summary>Derivatives only. -1 for futures.</summary>
    [JsonPropertyName("strikePrice")] public double? StrikePrice { get; init; }

    [JsonPropertyName("hasFnO")] public bool? HasFnO { get; init; }

    [JsonPropertyName("touchline")] public Touchline? Touchline { get; init; }
}
