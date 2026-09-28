// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using High.OpenApi.Http;
using Xunit;

namespace High.OpenApi.Tests;

public class PathTemplateTests
{
    [Fact]
    public void Interpolates_a_simple_value()
    {
        var path = PathTemplate.Of("/orders/{orderId}", new Dictionary<string, string> { ["orderId"] = "123" });
        Assert.Equal("/orders/123", path);
    }

    [Fact]
    public void Percent_encodes_an_ampersand_in_a_trading_symbol()
    {
        var path = PathTemplate.Of("/scrips/{symbol}/depth", new Dictionary<string, string> { ["symbol"] = "M&M-EQ" });
        Assert.Equal("/scrips/M%26M-EQ/depth", path);
    }

    [Fact]
    public void Percent_encodes_a_space_in_a_trading_symbol()
    {
        var path = PathTemplate.Of("/scrips/{symbol}/depth", new Dictionary<string, string> { ["symbol"] = "NIFTY 50" });
        Assert.Equal("/scrips/NIFTY%2050/depth", path);
    }

    [Fact]
    public void Percent_encodes_an_embedded_slash_so_it_cannot_split_the_path()
    {
        var path = PathTemplate.Of("/scrips/{symbol}/depth", new Dictionary<string, string> { ["symbol"] = "A/B" });
        Assert.Equal("/scrips/A%2FB/depth", path);
    }

    [Fact]
    public void Interpolates_two_parameters()
    {
        var path = PathTemplate.Of(
            "/scrips/{symbol}/{type}/expiries",
            new Dictionary<string, string> { ["symbol"] = "NIFTY 50", ["type"] = "options" });
        Assert.Equal("/scrips/NIFTY%2050/options/expiries", path);
    }

    [Fact]
    public void Throws_naming_the_missing_parameter()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            PathTemplate.Of("/orders/{orderId}", new Dictionary<string, string>()));
        Assert.Contains("orderId", ex.Message);
    }
}
