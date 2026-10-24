// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using System.Text.Json;
using High.OpenApi;
using High.OpenApi.Models;
using Xunit;

namespace High.OpenApi.Tests.Resources;

public class PortfolioResourceTests
{
    private static HighClient ClientFor(TestServer server) =>
        new(new HighClientOptions { BaseUrl = server.BaseUrl, AccessToken = "tok" });

    [Fact]
    public async Task Positions_snapshot_is_a_PnlSnapshot()
    {
        using var server = new TestServer((ctx, _) => TestServerResponses.EnvelopeAsync(ctx, 200, "r", """
            {"totalStocks":1,"snapshot":{"bookedPL":100,"unrealisedPL":123,"totalPL":223,
             "bookedPLPercent":0.71,"unrealisedPLPercent":0.88,"totalPLPercent":0.8},
             "positions":[],"scrips":{}}
            """));
        using var client = ClientFor(server);

        var positions = await client.Portfolio.Positions();
        Assert.Equal(223, positions.Snapshot.TotalPl);
        Assert.Equal("/v1/portfolio/positions", server.Requests[0].Url);
    }

    [Fact]
    public async Task Holdings_snapshot_is_a_differently_shaped_InvestmentSnapshot()
    {
        using var server = new TestServer((ctx, _) => TestServerResponses.EnvelopeAsync(ctx, 200, "r", """
            {"totalStocks":1,"snapshot":{"investment":13000,"currentValue":14123,"previousDayValue":14058,
             "totalPL":1123,"dayPL":65,"totalPLPercent":8.64,"dayPLPercent":0.46},
             "holdings":[],"scrips":{}}
            """));
        using var client = ClientFor(server);

        var holdings = await client.Portfolio.Holdings();
        Assert.Equal(13000, holdings.Snapshot.Investment);
        Assert.Equal("/v1/portfolio/holdings", server.Requests[0].Url);
    }

    [Fact]
    public async Task Funds_returns_nested_mtf_and_charge_blocks()
    {
        using var server = new TestServer((ctx, _) => TestServerResponses.EnvelopeAsync(ctx, 200, "r", """
            {"availableBalance":125000.5,"ledgerBalance":150000,"todaysBalance":155000.5,
             "marginUtilized":30000,"marginAgainstAssets":50000,"todaysPayIn":10000,"todaysPayout":5000,
             "mtfFunds":{"mtfCash":20000,"mtfFunded":60000,"totalMTFFunding":80000},
             "charges":{"delayedPaymentCharges":0,"dpCharges":14.75,"totalCharges":14.75},
             "unsettledFutureAmount":0}
            """));
        using var client = ClientFor(server);

        var funds = await client.Portfolio.Funds();
        Assert.Equal(80000, funds.MtfFunds.TotalMtfFunding);
        Assert.Equal(14.75, funds.Charges.TotalCharges);
    }

    [Fact]
    public async Task ConvertPosition_sends_PATCH_with_source_and_target_product_type_enums()
    {
        using var server = new TestServer((ctx, _) => TestServerResponses.EnvelopeAsync(ctx, 200, "r", """
            {"tradingSymbol":"RELIANCE-EQ","exchangeSegment":"NSE","scripCode":"2885","quantity":50,"isSubmitted":true}
            """));
        using var client = ClientFor(server);

        var result = await client.Portfolio.ConvertPosition(new ConvertPositionRequest
        {
            TradingSymbol = "RELIANCE-EQ", TradeSide = TradeSide.Buy, Quantity = 50,
            SourceProductType = ProductType.Intraday, TargetProductType = ProductType.Delivery,
        });

        Assert.True(result.IsSubmitted);
        Assert.Equal("PATCH", server.Requests[0].Method);
        var sent = JsonDocument.Parse(server.Requests[0].Body).RootElement;
        Assert.Equal("INTRADAY", sent.GetProperty("sourceProductType").GetString());
        Assert.Equal("DELIVERY", sent.GetProperty("targetProductType").GetString());
    }

    [Fact]
    public async Task ExitAllPositions_is_a_bodyless_DELETE()
    {
        using var server = new TestServer((ctx, _) => TestServerResponses.EnvelopeAsync(ctx, 200, "r", """
            {"totalCount":2,"successCount":1,"failureCount":1,"positions":[]}
            """));
        using var client = ClientFor(server);

        var result = await client.Portfolio.ExitAllPositions();
        Assert.Equal(2, result.TotalCount);
        Assert.Equal("DELETE", server.Requests[0].Method);
        Assert.Equal("", server.Requests[0].Body);
    }

    [Fact]
    public async Task ExitPosition_sends_a_body_on_DELETE_using_the_narrower_flavor_and_product_type_enums()
    {
        using var server = new TestServer((ctx, _) => TestServerResponses.EnvelopeAsync(ctx, 200, "r", """
            {"tradingSymbol":"RELIANCE-EQ","exchangeSegment":"NSE","scripCode":"2885","quantity":50,
             "flavor":"REGULAR","isSubmitted":false,"error":"Position could not be squared off. Please try again."}
            """));
        using var client = ClientFor(server);

        var result = await client.Portfolio.ExitPosition(new ExitPositionRequest
        {
            TradingSymbol = "RELIANCE-EQ", TradeSide = TradeSide.Sell, ProductType = ExitProductType.Intraday,
            Quantity = 50, Flavor = ExitFlavor.Regular,
        });

        Assert.False(result.IsSubmitted);
        Assert.Equal("Position could not be squared off. Please try again.", result.Error);
        Assert.Equal(Flavor.Regular, result.Flavor);
        Assert.Equal("DELETE", server.Requests[0].Method);
        Assert.NotEqual("", server.Requests[0].Body);

        var sent = JsonDocument.Parse(server.Requests[0].Body).RootElement;
        Assert.Equal("INTRADAY", sent.GetProperty("productType").GetString());
        Assert.Equal("REGULAR", sent.GetProperty("flavor").GetString());
    }

    [Fact]
    public async Task SetStoplossAsync_sends_PATCH_and_reads_the_placed_stop_leg()
    {
        using var server = new TestServer((ctx, _) => TestServerResponses.EnvelopeAsync(ctx, 200, "r", """
            {"tradingSymbol":"RELIANCE-EQ","exchangeSegment":"NSE","scripCode":"2885","leg":"STOPLOSS",
             "action":"PLACED","flavor":"REGULAR","orderId":"250512000012345","quantity":60,
             "price":2400,"limitPrice":2399.95,"isSubmitted":true}
            """));
        using var client = ClientFor(server);

        var result = await client.Portfolio.SetStoplossAsync(new SetStoplossRequest
        {
            TradingSymbol = "RELIANCE-EQ", Flavor = Flavor.Regular, ProductType = ExitProductType.Intraday, TriggerPrice = 2400,
        });

        Assert.Equal("/v1/portfolio/positions/stoploss", server.Requests[0].Url);
        Assert.Equal("PATCH", server.Requests[0].Method);
        Assert.Equal(RiskOrderLeg.StopLoss, result.Leg);
        Assert.Equal(RiskOrderAction.Placed, result.Action);
        Assert.Equal(2399.95, result.LimitPrice);
        Assert.True(result.IsSubmitted);

        var sent = JsonDocument.Parse(server.Requests[0].Body).RootElement;
        Assert.Equal("RELIANCE-EQ", sent.GetProperty("tradingSymbol").GetString());
        Assert.Equal("REGULAR", sent.GetProperty("flavor").GetString());
        Assert.Equal("INTRADAY", sent.GetProperty("productType").GetString());
        Assert.Equal(2400, sent.GetProperty("triggerPrice").GetDouble());
        Assert.False(sent.TryGetProperty("orderId", out _));
    }

    [Fact]
    public async Task SetStoplossAsync_by_segment_and_code_sends_the_amend_order_id_and_reads_a_modified_result()
    {
        using var server = new TestServer((ctx, _) => TestServerResponses.EnvelopeAsync(ctx, 200, "r", """
            {"tradingSymbol":"RELIANCE-EQ","exchangeSegment":"NSE","scripCode":"2885","leg":"STOPLOSS",
             "action":"MODIFIED","flavor":"GTT","orderId":"250512000012345","quantity":60,
             "price":2380,"limitPrice":2379.95,"isSubmitted":true}
            """));
        using var client = ClientFor(server);

        var result = await client.Portfolio.SetStoplossAsync(new SetStoplossRequest
        {
            ExchangeSegment = ExchangeSegment.Nse, ScripCode = "2885", Flavor = Flavor.Gtt,
            ProductType = ExitProductType.Delivery, OrderId = "250512000012345", TriggerPrice = 2380,
        });

        Assert.Equal(RiskOrderAction.Modified, result.Action);
        Assert.Equal(Flavor.Gtt, result.Flavor);

        var sent = JsonDocument.Parse(server.Requests[0].Body).RootElement;
        Assert.Equal("NSE", sent.GetProperty("exchangeSegment").GetString());
        Assert.Equal("2885", sent.GetProperty("scripCode").GetString());
        Assert.Equal("DELIVERY", sent.GetProperty("productType").GetString());
        Assert.Equal("250512000012345", sent.GetProperty("orderId").GetString());
        Assert.False(sent.TryGetProperty("tradingSymbol", out _));
    }

    [Fact]
    public async Task SetTargetAsync_sends_PATCH_with_target_price_and_reads_the_target_leg()
    {
        using var server = new TestServer((ctx, _) => TestServerResponses.EnvelopeAsync(ctx, 200, "r", """
            {"tradingSymbol":"RELIANCE-EQ","exchangeSegment":"NSE","scripCode":"2885","leg":"TARGET",
             "action":"PLACED","flavor":"REGULAR","orderId":"250512000012346","quantity":60,
             "price":2600,"isSubmitted":true}
            """));
        using var client = ClientFor(server);

        var result = await client.Portfolio.SetTargetAsync(new SetTargetRequest
        {
            TradingSymbol = "RELIANCE-EQ", Flavor = Flavor.Regular, ProductType = ExitProductType.Intraday, TargetPrice = 2600,
        });

        Assert.Equal("/v1/portfolio/positions/target", server.Requests[0].Url);
        Assert.Equal("PATCH", server.Requests[0].Method);
        Assert.Equal(RiskOrderLeg.Target, result.Leg);
        Assert.Equal(2600, result.Price);
        Assert.Null(result.LimitPrice);

        var sent = JsonDocument.Parse(server.Requests[0].Body).RootElement;
        Assert.Equal(2600, sent.GetProperty("targetPrice").GetDouble());
        Assert.False(sent.TryGetProperty("triggerPrice", out _));
    }
}
