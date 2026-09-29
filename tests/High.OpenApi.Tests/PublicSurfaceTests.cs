// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using System.Reflection;
using System.Text.Json;
using High.OpenApi;
using High.OpenApi.Resources;
using Xunit;

namespace High.OpenApi.Tests;

public class PublicSurfaceTests
{
    [Fact]
    public void Exposes_all_five_resources_on_a_constructed_client()
    {
        using var client = new HighClient(new HighClientOptions { AccessToken = "tok" });
        Assert.NotNull(client.Auth);
        Assert.NotNull(client.Market);
        Assert.NotNull(client.Orders);
        Assert.NotNull(client.Portfolio);
        Assert.NotNull(client.Scrips);
    }

    [Fact]
    public void Exposes_the_instruments_resource_with_exactly_a_streaming_and_an_eager_form()
    {
        using var client = new HighClient(new HighClientOptions { AccessToken = "tok" });
        Assert.NotNull(client.Instruments);

        // Not part of the 24-operation shared contract counted below: the
        // instrument list is resolved through an internal manifest, not a
        // directly wrapped REST operation, so it is asserted on its own.
        var publicMethodNames = typeof(InstrumentsResource)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.DeclaringType == typeof(InstrumentsResource))
            .Select(m => m.Name)
            .ToHashSet();

        Assert.Equal(new HashSet<string> { "Stream", "List" }, publicMethodNames);
    }

    [Fact]
    public void Exposes_every_operation_the_SDK_covers_as_a_public_method()
    {
        var expected = new Dictionary<Type, string[]>
        {
            [typeof(AuthResource)] = new[] { "GenerateAccessToken" },
            [typeof(OrdersResource)] = new[] { "Place", "Modify", "Get", "Cancel", "List", "Trades", "TradesFor", "Charges", "Margin" },
            [typeof(PortfolioResource)] = new[] { "Positions", "Holdings", "Funds", "ConvertPosition", "ExitAllPositions", "ExitPosition" },
            [typeof(ScripsResource)] = new[] { "Quotes", "Ohlc", "Depth", "Expiries", "FutureData", "Historical", "OptionChain" },
            [typeof(MarketResource)] = new[] { "Status" },
        };

        var count = 0;
        foreach (var (type, methods) in expected)
        {
            var publicMethodNames = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.DeclaringType == type)
                .Select(m => m.Name)
                .ToHashSet();

            foreach (var method in methods)
            {
                Assert.True(publicMethodNames.Contains(method), $"{type.Name}.{method} is missing");
                count++;
            }
        }

        // The spec has 27 operations; the SDK covers 24 — login, the two
        // consent operations and token introspection are deliberately excluded.
        Assert.Equal(24, count);
    }

    [Theory]
    [InlineData("Login")]
    [InlineData("RenderLogin")]
    [InlineData("LoginUrl")]
    [InlineData("GenerateConsent")]
    [InlineData("ConsumeConsent")]
    [InlineData("ValidateToken")]
    public void Wraps_neither_the_browser_login_page_the_consent_flow_nor_introspection(string excludedMethod)
    {
        var methodNames = typeof(AuthResource)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(m => m.Name);
        Assert.DoesNotContain(excludedMethod, methodNames);
    }

    [Fact]
    public void Credentials_do_not_appear_in_ToString_of_the_client()
    {
        using var client = new HighClient(new HighClientOptions { AccessToken = "SECRET-TOKEN", ApiKey = "SECRET-KEY" });
        var text = client.ToString();
        Assert.DoesNotContain("SECRET-TOKEN", text);
        Assert.DoesNotContain("SECRET-KEY", text);
    }

    [Fact]
    public void Credentials_do_not_appear_in_ToString_of_the_options()
    {
        var options = new HighClientOptions { AccessToken = "SECRET-TOKEN", ApiKey = "SECRET-KEY" };
        Assert.DoesNotContain("SECRET-TOKEN", options.ToString());
        Assert.DoesNotContain("SECRET-KEY", options.ToString());
    }

    [Fact]
    public void Credentials_do_not_appear_in_ToString_of_the_resolved_config()
    {
        using var client = new HighClient(new HighClientOptions { AccessToken = "SECRET-TOKEN", ApiKey = "SECRET-KEY" });
        Assert.DoesNotContain("SECRET-TOKEN", client.Config.ToString());
        Assert.DoesNotContain("SECRET-KEY", client.Config.ToString());
    }

    [Fact]
    public void Credentials_are_not_serialised_by_JsonSerializer()
    {
        using var client = new HighClient(new HighClientOptions { AccessToken = "SECRET-TOKEN", ApiKey = "SECRET-KEY" });
        var json = JsonSerializer.Serialize(client.Config);
        Assert.DoesNotContain("SECRET-TOKEN", json);
        Assert.DoesNotContain("SECRET-KEY", json);
    }

    [Fact]
    public void Credentials_stay_usable_despite_being_hidden()
    {
        using var client = new HighClient(new HighClientOptions { AccessToken = "SECRET-TOKEN" });
        Assert.Equal("SECRET-TOKEN", client.Config.AccessToken);
    }
}
