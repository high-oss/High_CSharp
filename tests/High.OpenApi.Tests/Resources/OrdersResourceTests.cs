// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using System.Text.Json;
using High.OpenApi;
using High.OpenApi.Models;
using Xunit;

namespace High.OpenApi.Tests.Resources;

public class OrdersResourceTests
{
    private static HighClient ClientFor(TestServer server) =>
        new(new HighClientOptions { BaseUrl = server.BaseUrl, AccessToken = "tok" });

    [Fact]
    public async Task Place_serialises_enums_as_their_exact_wire_strings()
    {
        using var server = new TestServer((ctx, _) =>
            TestServerResponses.EnvelopeAsync(ctx, 200, "r", """{"orderId":"1","error":""}"""));
        using var client = ClientFor(server);

        var result = await client.Orders.Place(new PlaceOrderRequest
        {
            TradeSide = TradeSide.Buy,
            TradingSymbol = "RELIANCE-EQ",
            ProductType = ProductType.Delivery,
            Flavor = Flavor.Regular,
            OrderType = OrderTypeEnum.Limit,
            Validity = Validity.Day,
            Quantity = 10,
            Price = 1410,
            IsAmo = false,
        });

        Assert.Equal("1", result.OrderId);
        Assert.Equal("", result.Error);

        var sent = JsonDocument.Parse(server.Requests[0].Body).RootElement;
        Assert.Equal("B", sent.GetProperty("tradeSide").GetString());
        Assert.Equal("DELIVERY", sent.GetProperty("productType").GetString());
        Assert.Equal("REGULAR", sent.GetProperty("flavor").GetString());
        Assert.Equal("LIMIT", sent.GetProperty("orderType").GetString());
        Assert.Equal("DAY", sent.GetProperty("validity").GetString());
        Assert.Equal("POST", server.Requests[0].Method);
        Assert.Equal("/v1/orders", server.Requests[0].Url);
        Assert.Equal("application/json; charset=utf-8", server.Requests[0].Headers["content-type"]);
    }

    [Fact]
    public async Task Modify_sends_PATCH_with_the_leg_indicator_enum()
    {
        using var server = new TestServer((ctx, _) =>
            TestServerResponses.EnvelopeAsync(ctx, 200, "r", """{"orderId":"1","error":""}"""));
        using var client = ClientFor(server);

        await client.Orders.Modify(new ModifyOrderRequest
        {
            OrderId = "1",
            TradeSide = TradeSide.Sell,
            TradingSymbol = "RELIANCE-EQ",
            ProductType = ProductType.Intraday,
            Flavor = Flavor.Bo,
            OrderType = OrderTypeEnum.StopLossMarket,
            Validity = Validity.Ioc,
            Quantity = 5,
            IsAmo = false,
            LegIndicator = LegIndicator.StopLossLeg,
        });

        Assert.Equal("PATCH", server.Requests[0].Method);
        var sent = JsonDocument.Parse(server.Requests[0].Body).RootElement;
        Assert.Equal("STOP_LOSS_MARKET", sent.GetProperty("orderType").GetString());
        Assert.Equal("STOP_LOSS_LEG", sent.GetProperty("legIndicator").GetString());
    }

    [Fact]
    public async Task Get_returns_an_order_book_with_a_null_error_and_string_scripCode()
    {
        using var server = new TestServer((ctx, _) => TestServerResponses.EnvelopeAsync(ctx, 200, "r", """
            {"orders":[{
                "orderId":"2609250000123456","tradeSide":"B","exchangeSegment":"NSE","scripCode":"2885",
                "flavor":"REGULAR","orderType":"LIMIT","validity":"DAY","productType":"DELIVERY","isAMO":false,
                "quantity":10,"tradedQty":10,"disclosedQuantity":0,"price":1410,"triggerPrice":0,
                "targetPrice":0,"stoplossPrice":0,"orderStatus":"Traded","error":null,
                "createdAt":"2026-09-25T05:00:00.000Z","updatedAt":"2026-09-25T05:00:01.000Z"
            }],"scrips":{"RELIANCE-EQ":{
                "tradingSymbol":"RELIANCE-EQ","scripCode":2885,"exchange":"NSE","segment":"ES",
                "scripKey":"NSE@2885","symbol":"RELIANCE","scripName":"Reliance Industries Ltd",
                "marketLot":1,"priceTick":10
            }}}
            """));
        using var client = ClientFor(server);

        var book = await client.Orders.Get("2609250000123456");

        var order = Assert.Single(book.Orders);
        Assert.Null(order.Error);
        Assert.Null(order.TradingSymbol);
        Assert.IsType<string>(order.ScripCode);
        Assert.Equal("2885", order.ScripCode);
        Assert.Equal(TradeSide.Buy, order.TradeSide);

        var scrip = book.Scrips["RELIANCE-EQ"];
        Assert.IsType<long>(scrip.ScripCode);
        Assert.Equal(2885L, scrip.ScripCode);

        Assert.Equal("GET", server.Requests[0].Method);
        Assert.Equal("/v1/orders/2609250000123456", server.Requests[0].Url);
    }

    [Fact]
    public async Task Cancel_percent_encodes_the_order_id_path_segment()
    {
        using var server = new TestServer((ctx, _) =>
            TestServerResponses.EnvelopeAsync(ctx, 200, "r", """{"orderId":"1","error":""}"""));
        using var client = ClientFor(server);

        var result = await client.Orders.Cancel("2609 250000123456");

        Assert.Equal("", result.Error);
        Assert.Equal("DELETE", server.Requests[0].Method);
        Assert.Equal("/v1/orders/2609%20250000123456", server.Requests[0].Url);
    }

    [Fact]
    public async Task List_and_Trades_and_TradesFor_hit_the_right_paths()
    {
        using var server = new TestServer((ctx, _) =>
        {
            var path = ctx.Request.Url!.AbsolutePath;
            return path.EndsWith("/trades")
                ? TestServerResponses.EnvelopeAsync(ctx, 200, "r", """{"trades":[],"scrips":{}}""")
                : TestServerResponses.EnvelopeAsync(ctx, 200, "r", """{"orders":[],"scrips":{}}""");
        });
        using var client = ClientFor(server);

        await client.Orders.List();
        await client.Orders.Trades();
        await client.Orders.TradesFor("XYZ");

        Assert.Equal("/v1/orders/list", server.Requests[0].Url);
        Assert.Equal("/v1/orders/trades", server.Requests[1].Url);
        Assert.Equal("/v1/orders/XYZ/trades", server.Requests[2].Url);
    }

    [Fact]
    public async Task Charges_returns_a_plain_string_productType_unlike_the_enum_request()
    {
        using var server = new TestServer((ctx, _) => TestServerResponses.EnvelopeAsync(ctx, 200, "r", """
            {"quantity":10,"price":1410,"productType":"DELIVERY","tradeSide":"B","tradedValue":14100,
             "charges":{"brokerage":5,"stt":14.1,"clearingCharges":0.141,"dpCharges":0,"stampDuty":2.115,
             "transactionCharges":0.423,"turnoverCharges":0.014,"gst":1.002,"totalBuyCharges":22.795,
             "totalSellCharges":35.43,"breakevenPrice":1415.823}}
            """));
        using var client = ClientFor(server);

        var charges = await client.Orders.Charges(new OrderChargesRequest
        {
            TradingSymbol = "RELIANCE-EQ", Quantity = 10, TradeSide = TradeSide.Buy, ProductType = ProductType.Delivery,
        });

        Assert.IsType<string>(charges.ProductType);
        Assert.Equal("DELIVERY", charges.ProductType);
        Assert.Equal(22.795, charges.Charges.TotalBuyCharges);
    }

    [Fact]
    public async Task Margin_sends_an_array_body_and_returns_the_span_summary()
    {
        using var server = new TestServer((ctx, _) => TestServerResponses.EnvelopeAsync(ctx, 200, "r", """
            {"margins":[{"tradingSymbol":"RELIANCE-EQ","scripKey":"NSE@2885","quantity":10,"tradeSide":"B",
             "initialMargin":14100,"exposureMargin":0}],
             "spanSummary":{"span":0,"exposureMargin":0,"optionPremium":0,"marginBenefit":0,"totalMargin":42150}}
            """));
        using var client = ClientFor(server);

        var margin = await client.Orders.Margin(new[]
        {
            new OrderMarginRequestLine { TradingSymbol = "RELIANCE-EQ", Quantity = 10, TradeSide = TradeSide.Buy, ProductType = ProductType.Delivery },
        });

        Assert.Equal(42150, margin.SpanSummary.TotalMargin);
        var sentBody = server.Requests[0].Body;
        Assert.StartsWith("[", sentBody.TrimStart());
    }
}
