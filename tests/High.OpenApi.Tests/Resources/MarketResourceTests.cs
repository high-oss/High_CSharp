// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using High.OpenApi;
using Xunit;

namespace High.OpenApi.Tests.Resources;

public class MarketResourceTests
{
    [Fact]
    public async Task Status_returns_exchange_status_keyed_by_exchange_code()
    {
        using var server = new TestServer((ctx, _) => TestServerResponses.EnvelopeAsync(ctx, 200, "r", """
            {"isHoliday":false,"date":"2026-09-25","exchangeStatus":{
                "NSE":{"status":"OPEN","timeRemainingInSeconds":12600},
                "MCX":{"status":"CLOSED","timeRemainingInSeconds":0,"isMock":true}
            }}
            """));
        using var client = new HighClient(new HighClientOptions { BaseUrl = server.BaseUrl, AccessToken = "tok" });

        var status = await client.Market.Status();

        Assert.False(status.IsHoliday);
        Assert.Equal("2026-09-25", status.Date);
        Assert.Equal("OPEN", status.ExchangeStatus["NSE"].Status);
        Assert.Equal(12600, status.ExchangeStatus["NSE"].TimeRemainingInSeconds);
        Assert.Null(status.ExchangeStatus["NSE"].IsMock);
        Assert.True(status.ExchangeStatus["MCX"].IsMock);
        Assert.Equal("GET", server.Requests[0].Method);
        Assert.Equal("/v1/market/status", server.Requests[0].Url);
    }
}
