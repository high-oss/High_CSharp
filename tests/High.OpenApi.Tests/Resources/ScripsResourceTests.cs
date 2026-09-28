// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using System.Text.Json;
using High.OpenApi;
using High.OpenApi.Models;
using Xunit;

namespace High.OpenApi.Tests.Resources;

public class ScripsResourceTests
{
    private static HighClient ClientFor(TestServer server) =>
        new(new HighClientOptions { BaseUrl = server.BaseUrl, AccessToken = "tok" });

    [Fact]
    public async Task Quotes_returns_a_map_keyed_by_trading_symbol_not_a_list()
    {
        using var server = new TestServer((ctx, _) => TestServerResponses.EnvelopeAsync(ctx, 200, "r", """
            {"RELIANCE-EQ":{"scrip":"NSE@2885","LTP":1412.3,"prevClose":1405.8,"change":6.5,"changePer":0.46}}
            """));
        using var client = ClientFor(server);

        var quotes = await client.Scrips.Quotes(new QuotesRequest { Symbols = new[] { "RELIANCE-EQ" } });

        Assert.Equal(1412.3, quotes["RELIANCE-EQ"].Ltp);
        Assert.Equal("POST", server.Requests[0].Method);
        var sent = JsonDocument.Parse(server.Requests[0].Body).RootElement;
        Assert.Equal("RELIANCE-EQ", sent.GetProperty("symbols")[0].GetString());
    }

    [Fact]
    public async Task Ohlc_map_entries_carry_an_ohlcv_block()
    {
        using var server = new TestServer((ctx, _) => TestServerResponses.EnvelopeAsync(ctx, 200, "r", """
            {"RELIANCE-EQ":{"scrip":"NSE@2885","LTP":1412.3,"prevClose":1405.8,"change":6.5,"changePer":0.46,
             "ohlcv":{"open":1407,"high":1418.6,"low":1403.2,"close":1412.3,"volume":6843210}}}
            """));
        using var client = ClientFor(server);

        var ohlc = await client.Scrips.Ohlc(new OhlcRequest { Symbols = new[] { "RELIANCE-EQ" } });
        Assert.Equal(6843210, ohlc["RELIANCE-EQ"].Ohlcv!.Volume);
    }

    [Fact]
    public async Task Depth_returns_five_levels_of_bids_and_asks()
    {
        using var server = new TestServer((ctx, _) => TestServerResponses.EnvelopeAsync(ctx, 200, "r", """
            {"tradingSymbol":"RELIANCE-EQ","bids":[{"price":1412.2,"quantity":850,"orderCount":12}],
             "asks":[],"totalBidQuantity":245630,"totalAskQuantity":312480,
             "totalAskPercentage":55.99,"totalBidsPercentage":44.01}
            """));
        using var client = ClientFor(server);

        var depth = await client.Scrips.Depth("RELIANCE-EQ");
        Assert.Single(depth.Bids);
        Assert.Equal("/v1/scrips/RELIANCE-EQ/depth", server.Requests[0].Url);
    }

    [Fact]
    public async Task Expiries_encodes_the_path_enum_case_correctly_for_both_values()
    {
        using var server = new TestServer((ctx, _) => TestServerResponses.EnvelopeAsync(ctx, 200, "r", """
            [{"expiry":"2026-09-29","type":"W"}]
            """));
        using var client = ClientFor(server);

        var expiries = await client.Scrips.Expiries("NIFTY 50", ExpiryType.Options);
        Assert.Equal(ExpiryFrequency.Weekly, Assert.Single(expiries).Type);
        Assert.Equal("/v1/scrips/NIFTY%2050/options/expiries", server.Requests[0].Url);

        await client.Scrips.Expiries("NIFTY 50", ExpiryType.Futures);
        Assert.Equal("/v1/scrips/NIFTY%2050/futures/expiries", server.Requests[1].Url);
    }

    [Fact]
    public async Task FutureData_returns_a_list_of_scrip_info_with_integer_scripCode()
    {
        using var server = new TestServer((ctx, _) => TestServerResponses.EnvelopeAsync(ctx, 200, "r", """
            [{"tradingSymbol":"RELIANCE-FUT","scripCode":88412,"exchange":"NSE","segment":"FO",
              "scripKey":"NSEFO@88412","symbol":"RELIANCE","scripName":"Reliance Industries Ltd",
              "marketLot":250,"priceTick":10}]
            """));
        using var client = ClientFor(server);

        var contracts = await client.Scrips.FutureData("RELIANCE-EQ");
        Assert.Equal(88412L, Assert.Single(contracts).ScripCode);
    }

    [Fact]
    public async Task Historical_returns_columnar_arrays_and_serialises_the_interval_enum_as_its_wire_string()
    {
        using var server = new TestServer((ctx, _) => TestServerResponses.EnvelopeAsync(ctx, 200, "r", """
            {"tradingSymbol":"NSE@2885","interval":"1D","timestamp":[1789929000,1790015400],
             "open":[1400,1410],"high":[1420,1425],"low":[1395,1405],"close":[1412,1418],"volume":[100,200]}
            """));
        using var client = ClientFor(server);

        var candles = await client.Scrips.Historical(new HistoricalRequest
        {
            TradingSymbol = "RELIANCE-EQ", Interval = HistoricalInterval.OneDay, FromTime = 1789929000, ToTime = 1790330400,
        });

        Assert.Equal(2, candles.Close.Count);
        Assert.Equal(1418, candles.Close[1]);

        var sent = JsonDocument.Parse(server.Requests[0].Body).RootElement;
        Assert.Equal("1D", sent.GetProperty("interval").GetString());
        Assert.Equal(1789929000, sent.GetProperty("fromTime").GetInt64());
    }

    [Fact]
    public async Task OptionChain_leg_may_be_an_empty_object_with_no_touchline()
    {
        using var server = new TestServer((ctx, _) => TestServerResponses.EnvelopeAsync(ctx, 200, "r", """
            {"baseStockInfo":{"tradingSymbol":"RELIANCE-EQ","scripCode":2885,"exchange":"NSE","segment":"ES",
              "scripKey":"NSE@2885","symbol":"RELIANCE","scripName":"Reliance Industries Ltd",
              "marketLot":1,"priceTick":10},
             "optionChain":[{"strikePrice":1400,"isATM":true,
                "call":{"tradingSymbol":"RELIANCE-29SEP26-1400-CE","scripKey":"NSEFO@88412","token":88412},
                "put":{}}],
             "marketLot":500,"maxOrderLots":36}
            """));
        using var client = ClientFor(server);

        var chain = await client.Scrips.OptionChain(new OptionChainRequest { TradingSymbol = "RELIANCE-EQ" });
        var row = Assert.Single(chain.Chain);
        Assert.True(row.IsAtm);
        Assert.NotNull(row.Call.TradingSymbol);
        Assert.Null(row.Call.Touchline);
        Assert.Null(row.Put.TradingSymbol);
        Assert.Null(row.Put.ScripKey);
        Assert.Null(row.Put.Token);
    }
}
