// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

namespace High.OpenApi.Feed;

/// <summary>
/// A complete, merged market-watch snapshot for one scrip, keyed by the
/// caller's own <see cref="ScripKey"/> — never the feed's own token. Ticks are
/// deltas; every property here reflects the last value seen for that field,
/// not only what changed on the most recent tick (see <see cref="FeedEvent.ChangedFields"/>
/// for that).
///
/// Top-of-book fields (best bid/ask) are deliberately not here — a FULL
/// market-watch tick carries them alongside these, but they are split out
/// into a one-level <see cref="FeedDepth"/> delivered through the depth
/// channel instead, so a caller reading a quote is never handed a
/// half-populated book and a caller reading a book never has to dig it out of
/// a quote.
/// </summary>
public sealed record Quote
{
    public required string ScripKey { get; init; }

    public string? TradingSymbol { get; init; }
    public decimal? LastTradedPrice { get; init; }
    public long? LastTradedQuantity { get; init; }

    /// <summary>IST wall-clock time, no offset — the wire carries none.</summary>
    public DateTime? LastTradedTime { get; init; }

    public long? Volume { get; init; }
    public decimal? Turnover { get; init; }
    public decimal? AverageTradePrice { get; init; }
    public decimal? Open { get; init; }
    public decimal? High { get; init; }
    public decimal? Low { get; init; }

    /// <summary>The prior session's close, not today's.</summary>
    public decimal? PreviousClose { get; init; }

    public decimal? YearHigh { get; init; }
    public decimal? YearLow { get; init; }

    /// <summary>Absolute change from <see cref="PreviousClose"/>.</summary>
    public decimal? Change { get; init; }

    /// <summary>Percentage change from <see cref="PreviousClose"/>.</summary>
    public decimal? ChangePercent { get; init; }

    public long? TotalBuyQuantity { get; init; }
    public long? TotalSellQuantity { get; init; }
    public long? OpenInterest { get; init; }
    public decimal? LowerCircuitLimit { get; init; }
    public decimal? UpperCircuitLimit { get; init; }

    /// <summary>IST wall-clock time, no offset.</summary>
    public DateTime? FeedTime { get; init; }

    public decimal? Multiplier { get; init; }
    public decimal? Precision { get; init; }

    /// <summary>
    /// Wire fields this SDK does not (yet) name, preserved rather than dropped
    /// so a vendor addition still reaches the caller. Keyed by the wire's own
    /// short field name.
    /// </summary>
    public IReadOnlyDictionary<string, string?> Extra { get; init; } = FeedFieldMaps.EmptyExtra;
}

/// <summary>One price level of a book: a price, the quantity resting there, and (when known) how many orders make it up.</summary>
public sealed record FeedDepthLevel
{
    public required decimal Price { get; init; }
    public required long Quantity { get; init; }

    /// <summary>
    /// Null when the level came from a market-watch tick's top-of-book fields,
    /// which never carry an order count — only the dedicated depth feed does.
    /// </summary>
    public long? OrderCount { get; init; }
}

/// <summary>Where a <see cref="FeedDepth"/> came from, which also tells a caller how many levels to expect.</summary>
public enum FeedDepthSource
{
    /// <summary>Top-of-book split out of a FULL market-watch tick. Always exactly one level.</summary>
    MarketWatch,

    /// <summary>The dedicated depth feed. Always five levels.</summary>
    Depth,
}

/// <summary>
/// A merged book for one scrip. <see cref="Source"/> and <see cref="LevelCount"/>
/// say which feed produced it — a one-level market-watch top-of-book must
/// never be presented as a five-level book with four empty rows, which would
/// read as a suddenly-illiquid instrument, so the two are never blurred into
/// one shape.
/// </summary>
public sealed record FeedDepth
{
    public required string ScripKey { get; init; }
    public required FeedDepthSource Source { get; init; }

    /// <summary>1 for <see cref="FeedDepthSource.MarketWatch"/>, 5 for <see cref="FeedDepthSource.Depth"/>.</summary>
    public required int LevelCount { get; init; }

    public required IReadOnlyList<FeedDepthLevel> Bids { get; init; }
    public required IReadOnlyList<FeedDepthLevel> Asks { get; init; }
}

/// <summary>A complete, merged index snapshot, keyed by the caller's own <see cref="ScripKey"/>.</summary>
public sealed record IndexQuote
{
    public required string ScripKey { get; init; }

    public string? IndexName { get; init; }
    public decimal? IndexValue { get; init; }

    /// <summary>The prior session's close, not today's.</summary>
    public decimal? PreviousClose { get; init; }

    public decimal? Open { get; init; }
    public decimal? High { get; init; }
    public decimal? Low { get; init; }
    public decimal? Change { get; init; }
    public decimal? ChangePercent { get; init; }

    /// <summary>IST wall-clock time, no offset.</summary>
    public DateTime? FeedTime { get; init; }

    /// <summary>Wire fields this SDK does not (yet) name, preserved rather than dropped.</summary>
    public IReadOnlyDictionary<string, string?> Extra { get; init; } = FeedFieldMaps.EmptyExtra;
}
