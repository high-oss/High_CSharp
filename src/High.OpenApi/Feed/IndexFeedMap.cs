// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

namespace High.OpenApi.Feed;

/// <summary>
/// One row of the committed index-to-feed translation table: which feed
/// segment and symbol name a HIGH index scrip key resolves to.
/// </summary>
/// <param name="Segment">The feed segment, e.g. <c>nse_cm</c>.</param>
/// <param name="Symbol">The feed's own name for the index, e.g. <c>Nifty 50</c>.</param>
public readonly record struct IndexFeedEntry(string Segment, string Symbol);

/// <summary>
/// Generated from <c>index-feed-map.json</c> (the canonical source at
/// <c>High/sdk/index-feed-map.json</c>, vendored into this repo at
/// <c>spec/index-feed-map.json</c>) -- DO NOT HAND-EDIT. Regenerate with the
/// same join described in the datafeed socket plan, and rerun
/// <c>IndexFeedMapGenerationTests</c> to confirm this file still matches.
///
/// The source is now an object, not a flat array: <see cref="Table"/> holds
/// the 81 unambiguous rows (<c>indices</c> in the source); <see cref="Ambiguous"/>
/// holds 6 scrip keys the scrip master maps to more than one index (one
/// token shared by two NSE indices) and are deliberately excluded from
/// <see cref="Table"/> -- resolving one of those by picking either candidate
/// would silently stream the wrong index under the caller's name, which is
/// worse than refusing outright, since the prices would look entirely
/// plausible. The index lookup runs before the prefix-derived rule in
/// <see cref="ScripKeyTranslator"/>, because indices share the <c>NSE@</c>/
/// <c>BSE@</c> prefix with cash scrips.
/// </summary>
internal static class IndexFeedMap
{
    public static readonly IReadOnlyDictionary<string, IndexFeedEntry> Table = new Dictionary<string, IndexFeedEntry>(System.StringComparer.Ordinal)
    {
        ["BSE@19000"] = new("bse_cm", "SENSEX"),
        ["BSE@19001"] = new("bse_cm", "BSEPSU"),
        ["BSE@19002"] = new("bse_cm", "BSE100"),
        ["BSE@19003"] = new("bse_cm", "BSE200"),
        ["BSE@19004"] = new("bse_cm", "BSE500"),
        ["BSE@19005"] = new("bse_cm", "BSE IT"),
        ["BSE@19006"] = new("bse_cm", "BSEFMC"),
        ["BSE@19007"] = new("bse_cm", "BSE CG"),
        ["BSE@19008"] = new("bse_cm", "BSE CD"),
        ["BSE@19009"] = new("bse_cm", "BSE HC"),
        ["BSE@19011"] = new("bse_cm", "TECK"),
        ["BSE@19012"] = new("bse_cm", "BANKEX"),
        ["BSE@19013"] = new("bse_cm", "AUTO"),
        ["BSE@19014"] = new("bse_cm", "METAL"),
        ["BSE@19015"] = new("bse_cm", "CPSE"),
        ["BSE@19016"] = new("bse_cm", "MIDCAP"),
        ["BSE@19017"] = new("bse_cm", "SMLCAP"),
        ["BSE@19019"] = new("bse_cm", "DOL100"),
        ["BSE@19020"] = new("bse_cm", "DOL200"),
        ["BSE@19051"] = new("bse_cm", "OILGAS"),
        ["BSE@19052"] = new("bse_cm", "POWER"),
        ["BSE@19053"] = new("bse_cm", "REALTY"),
        ["BSE@19054"] = new("bse_cm", "BSEIPO"),
        ["BSE@19059"] = new("bse_cm", "SMEIPO"),
        ["BSE@19060"] = new("bse_cm", "INFRA"),
        ["BSE@19089"] = new("bse_cm", "LMI250"),
        ["BSE@39"] = new("bse_cm", "ENERGY"),
        ["BSE@40"] = new("bse_cm", "FINSER"),
        ["BSE@41"] = new("bse_cm", "INDSTR"),
        ["BSE@42"] = new("bse_cm", "LRGCAP"),
        ["BSE@43"] = new("bse_cm", "MIDSEL"),
        ["BSE@44"] = new("bse_cm", "SMLSEL"),
        ["BSE@45"] = new("bse_cm", "TELCOM"),
        ["BSE@47"] = new("bse_cm", "SNSX50"),
        ["BSE@48"] = new("bse_cm", "SNXT50"),
        ["BSE@55"] = new("bse_cm", "MID150"),
        ["BSE@58"] = new("bse_cm", "MSL400"),
        ["NSE@25998"] = new("nse_cm", "Nifty 500"),
        ["NSE@26000"] = new("nse_cm", "Nifty 50"),
        ["NSE@26001"] = new("nse_cm", "Nifty GrowSect 15"),
        ["NSE@26008"] = new("nse_cm", "Nifty IT"),
        ["NSE@26009"] = new("nse_cm", "Nifty Bank"),
        ["NSE@26012"] = new("nse_cm", "Nifty 100"),
        ["NSE@26013"] = new("nse_cm", "Nifty Next 50"),
        ["NSE@26014"] = new("nse_cm", "Nifty Midcap 50"),
        ["NSE@26017"] = new("nse_cm", "India VIX"),
        ["NSE@26018"] = new("nse_cm", "Nifty Pharma"),
        ["NSE@26019"] = new("nse_cm", "Nifty Infra"),
        ["NSE@26021"] = new("nse_cm", "Nifty Realty"),
        ["NSE@26022"] = new("nse_cm", "Nifty MNC"),
        ["NSE@26024"] = new("nse_cm", "Nifty PSE"),
        ["NSE@26026"] = new("nse_cm", "Nifty Serv Sector"),
        ["NSE@26033"] = new("nse_cm", "Nifty Auto"),
        ["NSE@26035"] = new("nse_cm", "Nifty Consumption"),
        ["NSE@26036"] = new("nse_cm", "Nifty 200"),
        ["NSE@26037"] = new("nse_cm", "Nifty Fin Service"),
        ["NSE@26038"] = new("nse_cm", "Nifty50 Div Point"),
        ["NSE@26041"] = new("nse_cm", "Nifty CPSE"),
        ["NSE@26042"] = new("nse_cm", "Nifty50 PR 1x Inv"),
        ["NSE@26043"] = new("nse_cm", "Nifty50 TR 2x Lev"),
        ["NSE@26048"] = new("nse_cm", "NIFTY100 Qualty30"),
        ["NSE@26049"] = new("nse_cm", "Nifty GS 8 13Yr"),
        ["NSE@26050"] = new("nse_cm", "Nifty GS 10Yr"),
        ["NSE@26051"] = new("nse_cm", "Nifty GS 10Yr Cln"),
        ["NSE@26052"] = new("nse_cm", "Nifty GS 4 8Yr"),
        ["NSE@26053"] = new("nse_cm", "Nifty GS 11 15Yr"),
        ["NSE@26054"] = new("nse_cm", "Nifty GS 15YrPlus"),
        ["NSE@26055"] = new("nse_cm", "Nifty GS Compsite"),
        ["NSE@26056"] = new("nse_cm", "NIFTY50 EQL Wgt"),
        ["NSE@26057"] = new("nse_cm", "NIFTY100 EQL Wgt"),
        ["NSE@26058"] = new("nse_cm", "NIFTY100 LowVol30"),
        ["NSE@26059"] = new("nse_cm", "NIFTY Alpha 50"),
        ["NSE@26070"] = new("nse_cm", "Nifty50 Value 20"),
        ["NSE@26074"] = new("nse_cm", "NIFTY MID SELECT"),
        ["NSE@26076"] = new("nse_cm", "Nifty Pvt Bank"),
        ["NSE@26085"] = new("nse_cm", "Nifty Media"),
        ["NSE@26090"] = new("nse_cm", "NIFTY MIDCAP 150"),
        ["NSE@26091"] = new("nse_cm", "NIFTY SMLCAP 50"),
        ["NSE@26092"] = new("nse_cm", "NIFTY SMLCAP 250"),
        ["NSE@26093"] = new("nse_cm", "NIFTY MIDSML 400"),
        ["NSE@26094"] = new("nse_cm", "NIFTY200 QUALTY30"),
    };

    /// <summary>
    /// Scrip keys that resolve to more than one index in the scrip master.
    /// <see cref="ScripKeyTranslator.TranslateIndex"/> rejects these, naming the
    /// candidates, instead of guessing which one the caller meant.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Ambiguous = new Dictionary<string, IReadOnlyList<string>>(System.StringComparer.Ordinal)
    {
        ["NSE@26002"] = new[] { "Nifty FMCG", "Nifty50 PR 2x Lev" },
        ["NSE@26020"] = new[] { "Nifty Energy", "Nifty PSU Bank" },
        ["NSE@26034"] = new[] { "Nifty Div Opps 50", "Nifty Metal" },
        ["NSE@26040"] = new[] { "Nifty Commodities", "Nifty100 Liq 15" },
        ["NSE@26044"] = new[] { "NIFTY MIDCAP 100", "Nifty50 TR 1x Inv" },
        ["NSE@26046"] = new[] { "NIFTY SMLCAP 100", "Nifty Mid Liq 15" },
    };
}
