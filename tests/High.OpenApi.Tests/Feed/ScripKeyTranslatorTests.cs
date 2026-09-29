// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using High.OpenApi.Feed;
using Xunit;

namespace High.OpenApi.Tests.Feed;

public class ScripKeyTranslatorTests
{
    [Theory]
    [InlineData("NSE@15563", "nse_cm", "15563")]
    [InlineData("BSE@532540", "bse_cm", "532540")]
    [InlineData("NSEFO@52190", "nse_fo", "52190")]
    [InlineData("BSEFO@842150", "bse_fo", "842150")]
    [InlineData("MCXFO@227915", "mcx_fo", "227915")]
    public void TranslateQuoteOrDepth_derives_the_feed_segment_from_the_key_prefix(string key, string expectedSegment, string expectedIdentifier)
    {
        var (segment, identifier) = ScripKeyTranslator.TranslateQuoteOrDepth(key);
        Assert.Equal(expectedSegment, segment);
        Assert.Equal(expectedIdentifier, identifier);
    }

    [Fact]
    public void TranslateQuoteOrDepth_rejects_an_unknown_prefix_naming_the_key()
    {
        var ex = Assert.Throws<HighFeedError>(() => ScripKeyTranslator.TranslateQuoteOrDepth("XYZ@123"));
        Assert.Contains("XYZ@123", ex.Message);
    }

    [Fact]
    public void TranslateQuoteOrDepth_rejects_MCX_spot_naming_the_key()
    {
        var ex = Assert.Throws<HighFeedError>(() => ScripKeyTranslator.TranslateQuoteOrDepth("MCX@GOLD"));
        Assert.Contains("MCX@GOLD", ex.Message);
        Assert.Contains("MCX spot", ex.Message);
    }

    [Fact]
    public void TranslateQuoteOrDepth_rejects_a_key_the_index_table_knows_and_points_at_the_index_methods()
    {
        var ex = Assert.Throws<HighFeedError>(() => ScripKeyTranslator.TranslateQuoteOrDepth("BSE@19000"));
        Assert.Contains("BSE@19000", ex.Message);
        Assert.Contains("index key", ex.Message);
        Assert.Contains("SubscribeIndicesAsync", ex.Message);
    }

    [Fact]
    public void TranslateQuoteOrDepth_rejects_a_known_index_with_no_feed_counterpart_too()
    {
        // NSE@26016 is a known index (per the datafeed plan) that is deliberately
        // absent from the index table. The quote/depth path must still recognise
        // it as an index and refuse it, not silently treat it as an ordinary
        // NSE cash token.
        var ex = Assert.Throws<HighFeedError>(() => ScripKeyTranslator.TranslateQuoteOrDepth("NSE@26016"));
        Assert.Contains("NSE@26016", ex.Message);
        Assert.Contains("index key", ex.Message);
    }

    [Theory]
    [InlineData("BSE@19000", "bse_cm", "SENSEX")]
    [InlineData("NSE@26009", "nse_cm", "Nifty Bank")]
    public void TranslateIndex_resolves_a_known_index_key(string key, string expectedSegment, string expectedSymbol)
    {
        var (segment, symbol) = ScripKeyTranslator.TranslateIndex(key);
        Assert.Equal(expectedSegment, segment);
        Assert.Equal(expectedSymbol, symbol);
    }

    [Fact]
    public void TranslateIndex_rejects_an_ordinary_cash_scrip_key_naming_it_as_not_an_index()
    {
        var ex = Assert.Throws<HighFeedError>(() => ScripKeyTranslator.TranslateIndex("NSE@2885"));
        Assert.Contains("NSE@2885", ex.Message);
        Assert.Contains("not a known index", ex.Message);
    }

    [Fact]
    public void TranslateIndex_rejects_the_four_known_indices_with_no_feed_counterpart_with_a_reason()
    {
        var ex = Assert.Throws<HighFeedError>(() => ScripKeyTranslator.TranslateIndex("BSE@19018"));
        Assert.Contains("BSE@19018", ex.Message);
        Assert.Contains("DOLLEX", ex.Message);
    }

    [Fact]
    public void TranslateIndex_rejects_a_key_that_resolves_to_more_than_one_index_naming_both_candidates()
    {
        // NSE@26002 is one scrip-master token shared by two NSE indices (Nifty
        // FMCG and Nifty50 PR 2x Lev). Picking either by a last-wins rule would
        // silently stream the wrong index under a plausible-looking price, so
        // this must be refused rather than guessed at.
        var ex = Assert.Throws<HighFeedError>(() => ScripKeyTranslator.TranslateIndex("NSE@26002"));
        Assert.Contains("NSE@26002", ex.Message);
        Assert.Contains("Nifty FMCG", ex.Message);
        Assert.Contains("Nifty50 PR 2x Lev", ex.Message);
    }

    [Fact]
    public void TranslateQuoteOrDepth_rejects_an_ambiguous_index_key_too_rather_than_treating_it_as_a_cash_token()
    {
        var ex = Assert.Throws<HighFeedError>(() => ScripKeyTranslator.TranslateQuoteOrDepth("NSE@26002"));
        Assert.Contains("NSE@26002", ex.Message);
        Assert.Contains("index key", ex.Message);
    }

    [Fact]
    public void TranslateQuoteOrDepth_rejects_a_bare_key_with_no_prefix_separator()
    {
        var ex = Assert.Throws<HighFeedError>(() => ScripKeyTranslator.TranslateQuoteOrDepth("garbage"));
        Assert.Contains("garbage", ex.Message);
    }
}
