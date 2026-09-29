// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

namespace High.OpenApi.Feed;

/// <summary>
/// Translates a HIGH scrip key into the feed's own <c>segment|identifier</c>
/// pair. Every key is validated before translation, against a closed set of
/// supported prefixes plus the committed index table — never passed through
/// or guessed at, because a silent drop on a live feed is indistinguishable
/// from an instrument that simply is not trading.
///
/// Deliberately two entry points rather than one: <see cref="TranslateQuoteOrDepth"/>
/// refuses an index key (even one the feed cannot serve) and says so, and
/// <see cref="TranslateIndex"/> refuses anything that is not a known index. A
/// key found in the index table shares the <c>NSE@</c>/<c>BSE@</c> prefix with
/// ordinary cash scrips, so without this split an index key would otherwise
/// fall through the quote/depth path's prefix rule, resolve to a token the
/// feed does not recognise (e.g. <c>nse_cm|26000</c>), and the subscription
/// would be accepted and then simply never tick.
/// </summary>
internal static class ScripKeyTranslator
{
    private static readonly IReadOnlyDictionary<string, string> PrefixToSegment = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["NSE"] = "nse_cm",
        ["BSE"] = "bse_cm",
        ["NSEFO"] = "nse_fo",
        ["BSEFO"] = "bse_fo",
        ["MCXFO"] = "mcx_fo",
    };

    /// <summary>
    /// The four index rows the plan documents as having no feed counterpart,
    /// deliberately absent from <see cref="IndexFeedMap.Table"/> so a
    /// subscription attempt fails with a reason instead of silently never
    /// ticking.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> KnownIndicesWithNoFeedCounterpart = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["NSE@26016"] = "\"HANGSENG BEES-NAV\" is an ETF NAV pseudo-index, not traded.",
        ["BSE@19058"] = "\"S&P BSE CARBONEX\" is not published by the feed.",
        ["BSE@19081"] = "\"S&P BSE GREENEX\" is not published by the feed.",
        ["BSE@19018"] = "\"S&P BSE DOLLEX 30\" has no unambiguous match on the feed " +
                         "(it lists DOL100, DOL200 and DOL300 — guessing DOL300 is not a match to make on a hunch).",
    };

    /// <summary>For <c>SubscribeQuotesAsync</c>/<c>SubscribeDepthAsync</c>. Rejects any key the feed treats as an index.</summary>
    public static (string Segment, string Identifier) TranslateQuoteOrDepth(string scripKey)
    {
        if (IsKnownIndex(scripKey))
        {
            throw new HighFeedError(
                $"\"{scripKey}\" is an index key. Subscribe it with SubscribeIndicesAsync/UnsubscribeIndicesAsync, " +
                "not the quote or depth methods.");
        }

        return TranslateByPrefix(scripKey);
    }

    /// <summary>For <c>SubscribeIndicesAsync</c>. Accepts only a key present in the committed index table.</summary>
    public static (string Segment, string Identifier) TranslateIndex(string scripKey)
    {
        if (IndexFeedMap.Table.TryGetValue(scripKey, out var entry))
        {
            return (entry.Segment, entry.Symbol);
        }

        if (IndexFeedMap.Ambiguous.TryGetValue(scripKey, out var candidates))
        {
            throw new HighFeedError(
                $"\"{scripKey}\" resolves to more than one index in the scrip master: {string.Join(", ", candidates)}. " +
                "Picking one would silently stream the wrong index under this key; subscribe using the specific " +
                "index's own scrip key instead.");
        }

        if (KnownIndicesWithNoFeedCounterpart.TryGetValue(scripKey, out var reason))
        {
            throw new HighFeedError($"\"{scripKey}\" has no feed counterpart: {reason}");
        }

        throw new HighFeedError(
            $"\"{scripKey}\" is not a known index key. It is not in the committed table of " +
            $"{IndexFeedMap.Table.Count} indices the feed supports.");
    }

    private static bool IsKnownIndex(string scripKey) =>
        IndexFeedMap.Table.ContainsKey(scripKey) ||
        IndexFeedMap.Ambiguous.ContainsKey(scripKey) ||
        KnownIndicesWithNoFeedCounterpart.ContainsKey(scripKey);

    private static (string Segment, string Identifier) TranslateByPrefix(string scripKey)
    {
        if (string.IsNullOrWhiteSpace(scripKey))
        {
            throw new HighFeedError("A scrip key is required.");
        }

        var at = scripKey.IndexOf('@');
        if (at <= 0 || at == scripKey.Length - 1)
        {
            throw new HighFeedError($"\"{scripKey}\" is not a recognised HIGH scrip key (expected EXCHANGE@identifier).");
        }

        var prefix = scripKey[..at];
        var identifier = scripKey[(at + 1)..];

        if (string.Equals(prefix, "MCX", StringComparison.Ordinal))
        {
            throw new HighFeedError(
                $"\"{scripKey}\" is an MCX spot key. The datafeed has no known feed code for MCX spot; it cannot be subscribed.");
        }

        if (!PrefixToSegment.TryGetValue(prefix, out var segment))
        {
            throw new HighFeedError(
                $"\"{scripKey}\" has an unsupported prefix \"{prefix}\". Supported prefixes: " +
                $"{string.Join(", ", PrefixToSegment.Keys)}.");
        }

        return (segment, identifier);
    }
}
