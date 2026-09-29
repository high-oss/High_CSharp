// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using High.OpenApi.Json;

namespace High.OpenApi.Models;

// Every member below carries an explicit [EnumMember] naming the exact wire
// string, even where it looks like it would match the C# name by convention.
// SpecEnumConverter falls back to the C# member name when the attribute is
// absent, and that fallback is exactly the kind of silent-agreement-by-luck
// this SDK's payloads are not allowed to rely on.

/// <summary>Buy or sell. Shared by every request and response field the spec types as <c>enum: [B, S]</c>.</summary>
[JsonConverter(typeof(SpecEnumConverter<TradeSide>))]
public enum TradeSide
{
    [EnumMember(Value = "B")] Buy,
    [EnumMember(Value = "S")] Sell,
}

/// <summary>
/// Order/position flavor. 4-value form (<c>REGULAR, BO, CO, GTT</c>), used by
/// <c>Order</c>, <c>Position</c>, <c>PositionExitResult</c>, place and modify
/// requests. <c>ExitPositionRequest</c> uses the narrower <see cref="ExitFlavor"/>.
/// </summary>
[JsonConverter(typeof(SpecEnumConverter<Flavor>))]
public enum Flavor
{
    [EnumMember(Value = "REGULAR")] Regular,
    [EnumMember(Value = "BO")] Bo,
    [EnumMember(Value = "CO")] Co,
    [EnumMember(Value = "GTT")] Gtt,
}

/// <summary>The 3-value flavor <c>ExitPositionRequest.flavor</c> accepts: no <c>GTT</c>.</summary>
[JsonConverter(typeof(SpecEnumConverter<ExitFlavor>))]
public enum ExitFlavor
{
    [EnumMember(Value = "REGULAR")] Regular,
    [EnumMember(Value = "BO")] Bo,
    [EnumMember(Value = "CO")] Co,
}

/// <summary>
/// Product type. 3-value form (<c>INTRADAY, DELIVERY, MTF</c>), used by
/// <c>Order</c>, <c>Position</c>, place/modify/charges/margin requests and
/// position conversion. <c>ExitPositionRequest</c> uses the narrower
/// <see cref="ExitProductType"/>, and <c>OrderCharges</c>/<c>Trade</c> return
/// <c>productType</c> as a plain string (the spec gives it no enum there).
/// </summary>
[JsonConverter(typeof(SpecEnumConverter<ProductType>))]
public enum ProductType
{
    [EnumMember(Value = "INTRADAY")] Intraday,
    [EnumMember(Value = "DELIVERY")] Delivery,
    [EnumMember(Value = "MTF")] Mtf,
}

/// <summary>The 2-value product type <c>ExitPositionRequest.productType</c> accepts: no <c>MTF</c>.</summary>
[JsonConverter(typeof(SpecEnumConverter<ExitProductType>))]
public enum ExitProductType
{
    [EnumMember(Value = "INTRADAY")] Intraday,
    [EnumMember(Value = "DELIVERY")] Delivery,
}

/// <summary>Order type: <c>LIMIT, MARKET, STOP_LOSS, STOP_LOSS_MARKET</c>.</summary>
[JsonConverter(typeof(SpecEnumConverter<OrderTypeEnum>))]
public enum OrderTypeEnum
{
    [EnumMember(Value = "LIMIT")] Limit,
    [EnumMember(Value = "MARKET")] Market,
    [EnumMember(Value = "STOP_LOSS")] StopLoss,
    [EnumMember(Value = "STOP_LOSS_MARKET")] StopLossMarket,
}

/// <summary>
/// Order validity, as a place/modify request may set it: <c>DAY, IOC, YEAR</c>.
/// <c>Order.validity</c> in a response is a plain string — the broker can
/// return values outside this set, so the response model does not use this type.
/// </summary>
[JsonConverter(typeof(SpecEnumConverter<Validity>))]
public enum Validity
{
    [EnumMember(Value = "DAY")] Day,
    [EnumMember(Value = "IOC")] Ioc,
    [EnumMember(Value = "YEAR")] Year,
}

/// <summary>
/// The exchange segment a request targets when <c>tradingSymbol</c> is not
/// sent. <c>Order.exchangeSegment</c> in a response is a plain string — wider
/// than this set, since a book shared with other Choice channels can carry
/// segments this API does not accept on placement.
/// </summary>
[JsonConverter(typeof(SpecEnumConverter<ExchangeSegment>))]
public enum ExchangeSegment
{
    [EnumMember(Value = "NSE")] Nse,
    [EnumMember(Value = "BSE")] Bse,
    [EnumMember(Value = "NSEFO")] Nsefo,
    [EnumMember(Value = "BSEFO")] Bsefo,
    [EnumMember(Value = "NSECDSFO")] Nsecdsfo,
    [EnumMember(Value = "BSECDSFO")] Bsecdsfo,
    [EnumMember(Value = "MCXFO")] Mcxfo,
}

/// <summary>Which leg of a bracket/cover order <c>ModifyOrderRequest.legIndicator</c> targets.</summary>
[JsonConverter(typeof(SpecEnumConverter<LegIndicator>))]
public enum LegIndicator
{
    [EnumMember(Value = "ENTRY_LEG")] EntryLeg,
    [EnumMember(Value = "STOP_LOSS_LEG")] StopLossLeg,
    [EnumMember(Value = "TARGET_LEG")] TargetLeg,
}

/// <summary>
/// Which derivative family <c>scrips.Expiries</c> lists. Taken from the
/// spec's own path parameter enum, so widening it to <see cref="string"/>
/// would let a value the API rejects compile.
/// </summary>
[JsonConverter(typeof(SpecEnumConverter<ExpiryType>))]
public enum ExpiryType
{
    [EnumMember(Value = "futures")] Futures,
    [EnumMember(Value = "options")] Options,
}

/// <summary>Monthly or weekly, on <c>Expiry.type</c>. Distinct from <see cref="ExpiryType"/>, which names the derivative family instead.</summary>
[JsonConverter(typeof(SpecEnumConverter<ExpiryFrequency>))]
public enum ExpiryFrequency
{
    [EnumMember(Value = "M")] Monthly,
    [EnumMember(Value = "W")] Weekly,
}

/// <summary>
/// Which category of the instrument list (the scrip master) to read:
/// <c>instruments.Stream</c> and <c>instruments.List</c> both take one. Taken
/// from the spec's own <c>InstrumentFile.instrument</c> enum, so widening it
/// to <see cref="string"/> would let a category the manifest cannot serve
/// compile.
/// </summary>
[JsonConverter(typeof(SpecEnumConverter<InstrumentCategory>))]
public enum InstrumentCategory
{
    [EnumMember(Value = "all")] All,
    [EnumMember(Value = "equity")] Equity,
    [EnumMember(Value = "derivatives")] Derivatives,
    [EnumMember(Value = "commodity")] Commodity,
    [EnumMember(Value = "etfs")] Etfs,
}

/// <summary>Candle length for <c>scrips.Historical</c>. 3M, 6M and 1Y bypass the broker's usual translation and may come back empty — build them from 1M candles instead.</summary>
[JsonConverter(typeof(SpecEnumConverter<HistoricalInterval>))]
public enum HistoricalInterval
{
    [EnumMember(Value = "1m")] OneMinute,
    [EnumMember(Value = "2m")] TwoMinutes,
    [EnumMember(Value = "3m")] ThreeMinutes,
    [EnumMember(Value = "4m")] FourMinutes,
    [EnumMember(Value = "5m")] FiveMinutes,
    [EnumMember(Value = "10m")] TenMinutes,
    [EnumMember(Value = "15m")] FifteenMinutes,
    [EnumMember(Value = "30m")] ThirtyMinutes,
    [EnumMember(Value = "45m")] FortyFiveMinutes,
    [EnumMember(Value = "1h")] OneHour,
    [EnumMember(Value = "2h")] TwoHours,
    [EnumMember(Value = "3h")] ThreeHours,
    [EnumMember(Value = "4h")] FourHours,
    [EnumMember(Value = "1D")] OneDay,
    [EnumMember(Value = "1W")] OneWeek,
    [EnumMember(Value = "1M")] OneMonth,
    [EnumMember(Value = "3M")] ThreeMonths,
    [EnumMember(Value = "6M")] SixMonths,
    [EnumMember(Value = "1Y")] OneYear,
}
