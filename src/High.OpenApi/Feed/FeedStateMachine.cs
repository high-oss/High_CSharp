// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using System.Globalization;
using System.Text.Json.Nodes;

namespace High.OpenApi.Feed;

/// <summary>
/// Owns the per-instrument merge state and turns incoming tick frames into
/// typed events. Ticks are deltas — only changed fields are present — so this
/// merges each delta over whatever was last known for that instrument and
/// hands the caller a complete snapshot plus the set of fields that changed,
/// rather than a partial object with nulls for everything that simply didn't
/// move.
///
/// Routing is by each tick object's own <c>name</c> field (<c>sf</c> scrip,
/// <c>dp</c> depth, <c>if</c> index) — never by guessing from which keys are
/// present, which would misroute a depth-only frame that happens to share a
/// field name with a quote.
///
/// A FULL market-watch tick (<c>sf</c>) carries quote fields and top-of-book
/// fields together; this machine keeps two independent merge stores for a
/// single scrip key (<see cref="_quoteState"/> and <see cref="_topOfBookState"/>)
/// so the quote and the one-level book never contaminate each other's delta
/// history, and emits an event for a side only when that side's own fields
/// actually changed on this particular tick.
/// </summary>
internal sealed class FeedStateMachine
{
    private readonly Dictionary<string, JsonObject> _quoteState = new(StringComparer.Ordinal);
    private readonly Dictionary<string, JsonObject> _topOfBookState = new(StringComparer.Ordinal);
    private readonly Dictionary<string, JsonObject> _depthState = new(StringComparer.Ordinal);
    private readonly Dictionary<string, JsonObject> _indexState = new(StringComparer.Ordinal);
    private readonly object _lock = new();

    public event Action<QuoteEvent>? QuoteReceived;
    public event Action<DepthEvent>? DepthReceived;
    public event Action<IndexEvent>? IndexReceived;

    /// <param name="frame">One per-instrument tick object, already resolved to <paramref name="scripKey"/> by the caller (via <c>e</c>/<c>tk</c>).</param>
    /// <param name="scripKey">The caller's own scrip key — never the feed's token.</param>
    public void Dispatch(string scripKey, JsonObject frame)
    {
        var frameName = FeedFieldMaps.GetString(frame, "name");
        switch (frameName)
        {
            case "sf":
                HandleMarketWatch(scripKey, frame);
                break;
            case "dp":
                HandleDepth(scripKey, frame);
                break;
            case "if":
                HandleIndex(scripKey, frame);
                break;
        }
    }

    private void HandleMarketWatch(string scripKey, JsonObject frame)
    {
        var hasQuoteFields = frame.Any(kv =>
            !FeedFieldMaps.RoutingKeys.Contains(kv.Key) && !FeedFieldMaps.TopOfBookKeys.Contains(kv.Key));
        var hasTopOfBook = frame.Any(kv => FeedFieldMaps.TopOfBookKeys.Contains(kv.Key));

        if (hasQuoteFields)
        {
            var merged = Merge(_quoteState, scripKey, frame, excluding: FeedFieldMaps.TopOfBookKeys);
            var quote = BuildQuote(scripKey, merged);
            var changed = ChangedNames(frame, FeedFieldMaps.QuoteFieldMap);
            QuoteReceived?.Invoke(new QuoteEvent { ScripKey = scripKey, ChangedFields = changed, Quote = quote });
        }

        if (hasTopOfBook)
        {
            var merged = Merge(_topOfBookState, scripKey, frame, only: FeedFieldMaps.TopOfBookKeys);
            var depth = BuildTopOfBook(scripKey, merged);
            var changed = new List<string>(2);
            if (merged.ContainsKey("bp") || merged.ContainsKey("bq")) changed.Add(nameof(FeedDepth.Bids));
            if (merged.ContainsKey("sp") || merged.ContainsKey("bs")) changed.Add(nameof(FeedDepth.Asks));
            DepthReceived?.Invoke(new DepthEvent { ScripKey = scripKey, ChangedFields = changed, Depth = depth });
        }
    }

    private void HandleDepth(string scripKey, JsonObject frame)
    {
        var merged = Merge(_depthState, scripKey, frame, only: FeedFieldMaps.DepthKeys);
        var depth = BuildFiveLevelDepth(scripKey, merged);
        var changed = new[] { nameof(FeedDepth.Bids), nameof(FeedDepth.Asks) };
        DepthReceived?.Invoke(new DepthEvent { ScripKey = scripKey, ChangedFields = changed, Depth = depth });
    }

    private void HandleIndex(string scripKey, JsonObject frame)
    {
        var merged = Merge(_indexState, scripKey, frame, excluding: null);
        var index = BuildIndex(scripKey, merged);
        var changed = ChangedNames(frame, FeedFieldMaps.IndexFieldMap);
        IndexReceived?.Invoke(new IndexEvent { ScripKey = scripKey, ChangedFields = changed, Index = index });
    }

    /// <summary>
    /// Merges every field of <paramref name="delta"/> into the stored state for
    /// <paramref name="scripKey"/>, except <see cref="FeedFieldMaps.RoutingKeys"/>
    /// and whatever <paramref name="excluding"/> names — or, when <paramref name="only"/>
    /// is given, merges just those keys. Returns a snapshot clone so a caller
    /// reading it concurrently from <c>SnapshotAsync</c> never observes a
    /// partially-applied delta.
    /// </summary>
    private JsonObject Merge(Dictionary<string, JsonObject> store, string scripKey, JsonObject delta, IReadOnlySet<string>? excluding = null, IReadOnlySet<string>? only = null)
    {
        lock (_lock)
        {
            if (!store.TryGetValue(scripKey, out var existing))
            {
                existing = new JsonObject();
                store[scripKey] = existing;
            }

            foreach (var (key, value) in delta)
            {
                if (FeedFieldMaps.RoutingKeys.Contains(key)) continue;
                if (only is not null && !only.Contains(key)) continue;
                if (excluding is not null && excluding.Contains(key)) continue;
                existing[key] = value?.DeepClone();
            }

            return existing.DeepClone()!.AsObject();
        }
    }

    public JsonObject? RawQuoteState(string scripKey)
    {
        lock (_lock) return _quoteState.TryGetValue(scripKey, out var v) ? v.DeepClone()!.AsObject() : null;
    }

    public JsonObject? RawDepthState(string scripKey)
    {
        lock (_lock) return _depthState.TryGetValue(scripKey, out var v) ? v.DeepClone()!.AsObject() : null;
    }

    public JsonObject? RawIndexState(string scripKey)
    {
        lock (_lock) return _indexState.TryGetValue(scripKey, out var v) ? v.DeepClone()!.AsObject() : null;
    }

    private static List<string> ChangedNames(JsonObject delta, IReadOnlyDictionary<string, string> fieldMap)
    {
        var changed = new List<string>();
        foreach (var (key, _) in delta)
        {
            if (fieldMap.TryGetValue(key, out var name) && !changed.Contains(name)) changed.Add(name);
        }
        if (delta.Any(kv => !FeedFieldMaps.RoutingKeys.Contains(kv.Key) && !fieldMap.ContainsKey(kv.Key)))
        {
            changed.Add("Extra");
        }
        return changed;
    }

    internal static Quote BuildQuote(string scripKey, JsonObject merged) => new()
    {
        ScripKey = scripKey,
        TradingSymbol = FeedFieldMaps.GetString(merged, "ts"),
        LastTradedPrice = FeedFieldMaps.ParseDecimal(merged, "ltp"),
        LastTradedQuantity = FeedFieldMaps.ParseLong(merged, "ltq"),
        LastTradedTime = FeedFieldMaps.ParseExactTimestamp(merged, "ltt", "dd/MM/yyyy HH:mm:ss"),
        Volume = FeedFieldMaps.ParseLong(merged, "v"),
        Turnover = FeedFieldMaps.ParseDecimal(merged, "to"),
        AverageTradePrice = FeedFieldMaps.ParseDecimal(merged, "ap"),
        Open = FeedFieldMaps.ParseDecimal(merged, "op"),
        High = FeedFieldMaps.ParseDecimal(merged, "h"),
        Low = FeedFieldMaps.ParseDecimal(merged, "lo"),
        PreviousClose = FeedFieldMaps.ParseDecimal(merged, "c"),
        YearHigh = FeedFieldMaps.ParseDecimal(merged, "yh"),
        YearLow = FeedFieldMaps.ParseDecimal(merged, "yl"),
        Change = FeedFieldMaps.ParseDecimal(merged, "cng"),
        ChangePercent = FeedFieldMaps.ParseDecimal(merged, "nc"),
        TotalBuyQuantity = FeedFieldMaps.ParseLong(merged, "tbq"),
        TotalSellQuantity = FeedFieldMaps.ParseLong(merged, "tsq"),
        OpenInterest = FeedFieldMaps.ParseLong(merged, "oi"),
        LowerCircuitLimit = FeedFieldMaps.ParseDecimal(merged, "lcl"),
        UpperCircuitLimit = FeedFieldMaps.ParseDecimal(merged, "ucl"),
        FeedTime = FeedFieldMaps.ParseExactTimestamp(merged, "fdtm", "dd-MMM-yyyy HH:mm:ss"),
        Multiplier = FeedFieldMaps.ParseDecimal(merged, "mul"),
        Precision = FeedFieldMaps.ParseDecimal(merged, "prec"),
        Extra = FeedFieldMaps.ExtractExtra(merged, new HashSet<string>(FeedFieldMaps.QuoteFieldMap.Keys, StringComparer.Ordinal)),
    };

    internal static IndexQuote BuildIndex(string scripKey, JsonObject merged) => new()
    {
        ScripKey = scripKey,
        IndexName = FeedFieldMaps.GetString(merged, "ts") ?? FeedFieldMaps.GetString(merged, "tk"),
        IndexValue = FeedFieldMaps.ParseDecimal(merged, "iv"),
        PreviousClose = FeedFieldMaps.ParseDecimal(merged, "ic"),
        Open = FeedFieldMaps.ParseDecimal(merged, "openingprice"),
        High = FeedFieldMaps.ParseDecimal(merged, "highprice"),
        Low = FeedFieldMaps.ParseDecimal(merged, "lowprice"),
        Change = FeedFieldMaps.ParseDecimal(merged, "cng"),
        ChangePercent = FeedFieldMaps.ParseDecimal(merged, "nc"),
        FeedTime = FeedFieldMaps.ParseExactTimestamp(merged, "tvalue", "dd-MMM-yyyy HH:mm:ss"),
        Extra = FeedFieldMaps.ExtractExtra(merged, new HashSet<string>(FeedFieldMaps.IndexFieldMap.Keys, StringComparer.Ordinal)),
    };

    internal static FeedDepth BuildTopOfBook(string scripKey, JsonObject merged) => new()
    {
        ScripKey = scripKey,
        Source = FeedDepthSource.MarketWatch,
        LevelCount = 1,
        Bids = new[]
        {
            new FeedDepthLevel
            {
                Price = FeedFieldMaps.ParseDecimal(merged, "bp") ?? 0m,
                Quantity = FeedFieldMaps.ParseLong(merged, "bq") ?? 0L,
                OrderCount = null,
            },
        },
        Asks = new[]
        {
            new FeedDepthLevel
            {
                Price = FeedFieldMaps.ParseDecimal(merged, "sp") ?? 0m,
                Quantity = FeedFieldMaps.ParseLong(merged, "bs") ?? 0L,
                OrderCount = null,
            },
        },
    };

    internal static FeedDepth BuildFiveLevelDepth(string scripKey, JsonObject merged) => new()
    {
        ScripKey = scripKey,
        Source = FeedDepthSource.Depth,
        LevelCount = 5,
        Bids = BuildLevels(merged, pricePrefix: "bp", qtyPrefix: "bq", orderPrefix: "bno"),
        Asks = BuildLevels(merged, pricePrefix: "sp", qtyPrefix: "bs", orderPrefix: "sno"),
    };

    /// <summary>
    /// Level <c>n</c> (1-based) pairs price/quantity suffix <c>n-1</c>
    /// (unnumbered for level 1: <c>bp</c>, then <c>bp1</c>..<c>bp4</c>) with
    /// order-count suffix <c>n</c> (<c>bno1</c>..<c>bno5</c>). Getting this
    /// wrong silently attributes every level's order count to the wrong price.
    /// </summary>
    private static IReadOnlyList<FeedDepthLevel> BuildLevels(JsonObject merged, string pricePrefix, string qtyPrefix, string orderPrefix)
    {
        var levels = new List<FeedDepthLevel>(5);
        for (var level = 1; level <= 5; level++)
        {
            var priceQtySuffix = level == 1 ? string.Empty : (level - 1).ToString(CultureInfo.InvariantCulture);
            var orderSuffix = level.ToString(CultureInfo.InvariantCulture);

            levels.Add(new FeedDepthLevel
            {
                Price = FeedFieldMaps.ParseDecimal(merged, pricePrefix + priceQtySuffix) ?? 0m,
                Quantity = FeedFieldMaps.ParseLong(merged, qtyPrefix + priceQtySuffix) ?? 0L,
                OrderCount = FeedFieldMaps.ParseLong(merged, orderPrefix + orderSuffix),
            });
        }
        return levels;
    }
}
