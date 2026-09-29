// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using High.OpenApi;
using High.OpenApi.Logging;
using Xunit;

namespace High.OpenApi.Tests;

public class ConfigTests
{
    private static Func<string, string?> EnvOf(params (string Key, string Value)[] pairs)
    {
        var map = pairs.ToDictionary(p => p.Key, p => (string?)p.Value);
        return key => map.TryGetValue(key, out var value) ? value : null;
    }

    private static readonly Func<string, string?> NoEnv = _ => null;

    [Fact]
    public void Defaults_are_production_v1_and_silent()
    {
        var config = ConfigResolver.Resolve(new HighClientOptions(), NoEnv);

        Assert.Equal("https://openapi.high.live", config.BaseUrl);
        Assert.Equal("wss://openapi-feed.high.live", config.WsBaseUrl);
        Assert.Equal("v1", config.VersionPath);
        Assert.Equal(30_000, config.TimeoutMs);
        Assert.Equal(2, config.MaxRetries);
        Assert.Equal(30_000, config.MaxRetryDelayMs);
        Assert.Equal(LogLevel.Silent, config.LogLevel);
    }

    [Fact]
    public void Sandbox_environment_selects_the_sandbox_hosts()
    {
        var config = ConfigResolver.Resolve(new HighClientOptions { Environment = Environment.Sandbox }, NoEnv);
        Assert.Equal("https://sandbox.high.live", config.BaseUrl);
        Assert.Equal("wss://sandbox.high.live", config.WsBaseUrl);
    }

    [Fact]
    public void An_explicit_environment_beats_HIGH_BASE_URL()
    {
        var env = EnvOf(("HIGH_BASE_URL", "https://openapi.high.live"));
        var config = ConfigResolver.Resolve(new HighClientOptions { Environment = Environment.Sandbox }, env);
        Assert.Equal("https://sandbox.high.live", config.BaseUrl);
    }

    [Fact]
    public void An_explicit_environment_beats_HIGH_WS_BASE_URL_too_so_the_hosts_cannot_split()
    {
        var env = EnvOf(("HIGH_BASE_URL", "https://openapi.high.live"), ("HIGH_WS_BASE_URL", "wss://openapi.high.live"));
        var config = ConfigResolver.Resolve(new HighClientOptions { Environment = Environment.Sandbox }, env);
        Assert.Equal("https://sandbox.high.live", config.BaseUrl);
        Assert.Equal("wss://sandbox.high.live", config.WsBaseUrl);
    }

    [Fact]
    public void HIGH_BASE_URL_still_applies_when_no_environment_is_given()
    {
        var env = EnvOf(("HIGH_BASE_URL", "http://localhost:9999"));
        var config = ConfigResolver.Resolve(new HighClientOptions(), env);
        Assert.Equal("http://localhost:9999", config.BaseUrl);
    }

    [Fact]
    public void An_explicit_baseUrl_still_wins_over_everything()
    {
        var env = EnvOf(("HIGH_BASE_URL", "https://openapi.high.live"));
        var config = ConfigResolver.Resolve(
            new HighClientOptions { Environment = Environment.Sandbox, BaseUrl = "http://localhost:1" }, env);
        Assert.Equal("http://localhost:1", config.BaseUrl);
    }

    [Fact]
    public void HIGH_ENVIRONMENT_applies_when_no_explicit_environment_is_given()
    {
        var env = EnvOf(("HIGH_ENVIRONMENT", "sandbox"));
        var config = ConfigResolver.Resolve(new HighClientOptions(), env);
        Assert.Equal("https://sandbox.high.live", config.BaseUrl);
    }

    [Fact]
    public void Unknown_environment_option_fails_at_construction_naming_valid_values()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ConfigResolver.Resolve(new HighClientOptions { Environment = (Environment)99 }, NoEnv));
        Assert.Contains("production, sandbox", ex.Message);
    }

    [Fact]
    public void Unknown_HIGH_ENVIRONMENT_env_var_fails_at_construction()
    {
        var env = EnvOf(("HIGH_ENVIRONMENT", "staging"));
        var ex = Assert.Throws<ArgumentException>(() => ConfigResolver.Resolve(new HighClientOptions(), env));
        Assert.Contains("staging", ex.Message);
        Assert.Contains("production, sandbox", ex.Message);
    }

    [Fact]
    public void Unknown_HIGH_LOG_LEVEL_env_var_fails_at_construction()
    {
        var env = EnvOf(("HIGH_LOG_LEVEL", "verbose"));
        var ex = Assert.Throws<ArgumentException>(() => ConfigResolver.Resolve(new HighClientOptions(), env));
        Assert.Contains("verbose", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_positive_timeout_fails_at_construction(int timeout)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ConfigResolver.Resolve(new HighClientOptions { TimeoutMs = timeout }, NoEnv));
        Assert.Contains("TimeoutMs", ex.Message);
    }

    [Fact]
    public void Negative_maxRetries_fails_at_construction_rather_than_crashing_mid_request()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ConfigResolver.Resolve(new HighClientOptions { MaxRetries = -1 }, NoEnv));
        Assert.Contains("MaxRetries", ex.Message);
    }

    [Fact]
    public void Non_positive_maxRetryDelayMs_fails_at_construction()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ConfigResolver.Resolve(new HighClientOptions { MaxRetryDelayMs = 0 }, NoEnv));
        Assert.Contains("MaxRetryDelayMs", ex.Message);
    }

    [Fact]
    public void Credentials_fall_back_to_environment_variables()
    {
        var env = EnvOf(("HIGH_API_KEY", "env-key"), ("HIGH_ACCESS_TOKEN", "env-token"));
        var config = ConfigResolver.Resolve(new HighClientOptions(), env);
        Assert.Equal("env-key", config.ApiKey);
        Assert.Equal("env-token", config.AccessToken);
    }

    [Fact]
    public void Explicit_credentials_win_over_environment_variables()
    {
        var env = EnvOf(("HIGH_API_KEY", "env-key"));
        var config = ConfigResolver.Resolve(new HighClientOptions { ApiKey = "explicit-key" }, env);
        Assert.Equal("explicit-key", config.ApiKey);
    }

    [Theory]
    [InlineData("https://api.example.com", "v1", "/orders", "https://api.example.com/v1/orders")]
    [InlineData("https://api.example.com/", "/v1/", "//orders/", "https://api.example.com/v1/orders")]
    [InlineData("https://api.example.com", "", "/market/status", "https://api.example.com/market/status")]
    public void BuildUrl_is_tolerant_of_stray_slashes(string baseUrl, string versionPath, string path, string expected)
    {
        var config = ConfigResolver.Resolve(new HighClientOptions { BaseUrl = baseUrl, VersionPath = versionPath }, NoEnv);
        Assert.Equal(expected, config.BuildUrl(path));
    }

    [Fact]
    public void UserAgent_appends_to_the_SDKs_own()
    {
        var config = ConfigResolver.Resolve(new HighClientOptions { UserAgent = "my-app/1.0" }, NoEnv);
        Assert.StartsWith("high-sdk-csharp/0.0.1", config.UserAgent);
        Assert.Contains("my-app/1.0", config.UserAgent);
    }
}
