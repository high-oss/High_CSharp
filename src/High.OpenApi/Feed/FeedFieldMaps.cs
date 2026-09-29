// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using System.Globalization;
using System.Text.Json.Nodes;

namespace High.OpenApi.Feed;

/// <summary>
/// The wire-to-friendly field vocabulary from the datafeed socket plan,
/// verbatim, plus the small parsing helpers every tick handler shares:
/// decimal (never a binary float — these are prices), integer, and
/// exact-format timestamp parsing with leading-space trimming.
/// </summary>
internal static class FeedFieldMaps
{
    public static readonly IReadOnlyDictionary<string, string?> EmptyExtra = new Dictionary<string, string?>();

    /// <summary>Routing-only fields that never appear as a named property or in <c>Extra</c>.</summary>
    public static readonly IReadOnlySet<string> RoutingKeys = new HashSet<string>(StringComparer.Ordinal) { "e", "tk", "name" };

    /// <summary>
    /// Top-of-book fields split out of a FULL market-watch tick into a
    /// one-level <see cref="FeedDepth"/>. Deliberately excluded from the quote
    /// model and from the quote's <c>Extra</c> — they belong to the depth side.
    /// </summary>
    public static readonly IReadOnlySet<string> TopOfBookKeys = new HashSet<string>(StringComparer.Ordinal) { "bp", "bq", "sp", "bs" };

    /// <summary>Quote (market-watch) wire keys this SDK names, per the plan's field table. <c>cng</c> is absolute change; <c>nc</c> is the percentage — the opposite of the vendor sample's own labels.</summary>
    public static readonly IReadOnlyDictionary<string, string> QuoteFieldMap = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ts"] = nameof(Quote.TradingSymbol),
        ["ltp"] = nameof(Quote.LastTradedPrice),
        ["ltq"] = nameof(Quote.LastTradedQuantity),
        ["ltt"] = nameof(Quote.LastTradedTime),
        ["v"] = nameof(Quote.Volume),
        ["to"] = nameof(Quote.Turnover),
        ["ap"] = nameof(Quote.AverageTradePrice),
        ["op"] = nameof(Quote.Open),
        ["h"] = nameof(Quote.High),
        ["lo"] = nameof(Quote.Low),
        ["c"] = nameof(Quote.PreviousClose),
        ["yh"] = nameof(Quote.YearHigh),
        ["yl"] = nameof(Quote.YearLow),
        ["cng"] = nameof(Quote.Change),
        ["nc"] = nameof(Quote.ChangePercent),
        ["tbq"] = nameof(Quote.TotalBuyQuantity),
        ["tsq"] = nameof(Quote.TotalSellQuantity),
        ["oi"] = nameof(Quote.OpenInterest),
        ["lcl"] = nameof(Quote.LowerCircuitLimit),
        ["ucl"] = nameof(Quote.UpperCircuitLimit),
        ["fdtm"] = nameof(Quote.FeedTime),
        ["mul"] = nameof(Quote.Multiplier),
        ["prec"] = nameof(Quote.Precision),
    };

    /// <summary>Index wire keys this SDK names. <c>tk</c>/<c>ts</c> both carry the index name; <c>ts</c> wins when both are present.</summary>
    public static readonly IReadOnlyDictionary<string, string> IndexFieldMap = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["tk"] = nameof(IndexQuote.IndexName),
        ["ts"] = nameof(IndexQuote.IndexName),
        ["iv"] = nameof(IndexQuote.IndexValue),
        ["ic"] = nameof(IndexQuote.PreviousClose),
        ["openingprice"] = nameof(IndexQuote.Open),
        ["highprice"] = nameof(IndexQuote.High),
        ["lowprice"] = nameof(IndexQuote.Low),
        ["cng"] = nameof(IndexQuote.Change),
        ["nc"] = nameof(IndexQuote.ChangePercent),
        ["tvalue"] = nameof(IndexQuote.FeedTime),
    };

    /// <summary>
    /// Every wire key the five-level depth feed carries. Level <c>n</c> pairs
    /// price/quantity suffix <c>n-1</c> (unnumbered for level 1) with order-count
    /// suffix <c>n</c> — the off-by-one the plan calls out explicitly.
    /// </summary>
    public static readonly IReadOnlySet<string> DepthKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        "bp", "bp1", "bp2", "bp3", "bp4",
        "bq", "bq1", "bq2", "bq3", "bq4",
        "bno1", "bno2", "bno3", "bno4", "bno5",
        "sp", "sp1", "sp2", "sp3", "sp4",
        "bs", "bs1", "bs2", "bs3", "bs4",
        "sno1", "sno2", "sno3", "sno4", "sno5",
    };

    public static string? GetString(JsonObject obj, string key)
    {
        if (!obj.TryGetPropertyValue(key, out var node) || node is not JsonValue value) return null;

        var text = value.TryGetValue<string>(out var s) ? s : value.ToJsonString();
        text = text.Trim(' ', '"');
        return text.Length == 0 ? null : text;
    }

    public static decimal? ParseDecimal(JsonObject obj, string key)
    {
        var raw = GetString(obj, key);
        if (raw is null) return null;
        if (decimal.TryParse(raw, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }
        throw new HighFeedError($"Feed field \"{key}\" (\"{raw}\") could not be parsed as a decimal.");
    }

    public static long? ParseLong(JsonObject obj, string key)
    {
        var raw = GetString(obj, key);
        if (raw is null) return null;
        if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) return value;
        // A quantity field occasionally arrives with a decimal point; truncating
        // it is safer than failing the whole tick over a cosmetic difference.
        if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var asDecimal)) return (long)asDecimal;
        throw new HighFeedError($"Feed field \"{key}\" (\"{raw}\") could not be parsed as an integer.");
    }

    /// <summary>Parses with the field's own exact format, trimming leading spaces first (the vendor sample shows them on numeric fields; timestamps get the same treatment for safety).</summary>
    public static DateTime? ParseExactTimestamp(JsonObject obj, string key, string format)
    {
        var raw = GetString(obj, key);
        if (raw is null) return null;
        if (DateTime.TryParseExact(raw, format, CultureInfo.InvariantCulture, DateTimeStyles.NoCurrentDateDefault, out var value))
        {
            // IST wall-clock, no offset on the wire — Unspecified, not Utc/Local,
            // so nothing downstream silently reinterprets it as UTC.
            return DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
        }
        throw new HighFeedError($"Feed field \"{key}\" (\"{raw}\") did not match the expected format \"{format}\".");
    }

    public static IReadOnlyDictionary<string, string?> ExtractExtra(JsonObject merged, IReadOnlySet<string> known)
    {
        Dictionary<string, string?>? extra = null;
        foreach (var (key, value) in merged)
        {
            if (RoutingKeys.Contains(key) || known.Contains(key)) continue;
            extra ??= new Dictionary<string, string?>(StringComparer.Ordinal);
            extra[key] = value?.ToJsonString();
        }
        return extra ?? EmptyExtra;
    }
}
